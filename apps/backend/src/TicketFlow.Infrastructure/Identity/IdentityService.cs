using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Application.Auth;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Identity;

public sealed class IdentityService(TicketFlowDbContext db, UserManager<AppUser> users,
    SignInManager<AppUser> signIn, AuthSettings settings, IPasswordResetSender resetSender) : IIdentityService
{
    public async Task<AuthGrant> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = Email(request.Email);
        var name = request.Name?.Trim() ?? "";
        if (name.Length is < 2 or > 80 || request.Password is null || request.Password.Length is < 12 or > 128)
            throw new RuleException(ErrorKind.Validation, "Use a name of 2–80 characters and a strong password of 12–128 characters.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = name };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded) throw new RuleException(ErrorKind.Validation, "Unable to register. Use a unique email and a strong password.");
        var role = await users.AddToRoleAsync(user, "Customer");
        if (!role.Succeeded) throw new InvalidOperationException("Customer role assignment failed.");
        var grant = await GrantAsync(user, Guid.NewGuid(), ct);
        await transaction.CommitAsync(ct);
        return grant;
    }

    public async Task<AuthGrant> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Email(request.Email);
        var user = await users.FindByEmailAsync(email);
        if (user is null || request.Password is null || request.Password.Length > 128)
            throw new RuleException(ErrorKind.Unauthorized, "Invalid email or password.");
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded) throw new RuleException(ErrorKind.Unauthorized, "Invalid email or password.");
        return await GrantAsync(user, Guid.NewGuid(), ct);
    }

    public async Task<AuthGrant> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 200)
            throw new RuleException(ErrorKind.Unauthorized, "Your session expired. Please sign in.");
        var hash = Hash(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var session = await db.RefreshSessions.FromSqlInterpolated(
            $"SELECT * FROM refresh_sessions WHERE token_hash={hash} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (session is null) throw new RuleException(ErrorKind.Unauthorized, "Your session expired. Please sign in.");
        var now = DateTimeOffset.UtcNow;
        if (session.RevokedAt.HasValue || session.ExpiresAt <= now)
        {
            await RevokeFamilyAsync(session.UserId, session.FamilyId, ct);
            await transaction.CommitAsync(ct);
            throw new RuleException(ErrorKind.Unauthorized, "Your session expired. Please sign in.");
        }
        var user = await users.FindByIdAsync(session.UserId.ToString());
        if (user is null) throw new RuleException(ErrorKind.Unauthorized, "Your session expired. Please sign in.");
        session.RevokedAt = now;
        var grant = await GrantAsync(user, session.FamilyId, ct);
        await transaction.CommitAsync(ct);
        return grant;
    }

    public async Task LogoutAsync(string? refreshToken, Guid? userId, Guid? familyId, CancellationToken ct)
    {
        if (userId.HasValue && familyId.HasValue)
        {
            await RevokeFamilyAsync(userId.Value, familyId.Value, ct);
            return;
        }
        if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length > 200) return;
        var hash = Hash(refreshToken);
        var session = await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (session is not null) await RevokeFamilyAsync(session.UserId, session.FamilyId, ct);
    }

    public async Task<UserView?> FindUserAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(id.ToString());
        return user is null ? null : await ViewAsync(user);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 254) return;
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) return;
        var token = await users.GeneratePasswordResetTokenAsync(user);
        await resetSender.SendAsync(user.Email!, WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)), ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        if (request.Password is null || request.Password.Length is < 12 or > 128
            || string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 4096)
            throw new RuleException(ErrorKind.Validation, "Use a valid reset link and a strong password of 12–128 characters.");
        var user = await users.FindByEmailAsync(Email(request.Email));
        if (user is null) throw new RuleException(ErrorKind.Validation, "This reset link is invalid or expired.");
        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token)); }
        catch (FormatException) { throw new RuleException(ErrorKind.Validation, "This reset link is invalid or expired."); }
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await users.ResetPasswordAsync(user, token, request.Password);
        if (!result.Succeeded) throw new RuleException(ErrorKind.Validation, "This reset link is invalid, expired, or the password is not strong enough.");
        await db.SessionFamilies.Where(f => f.UserId == user.Id && f.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(f => f.RevokedAt, DateTimeOffset.UtcNow), ct);
        await db.RefreshSessions.Where(s => s.UserId == user.Id && s.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(s => s.RevokedAt, DateTimeOffset.UtcNow), ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<AuthGrant> GrantAsync(AppUser user, Guid family, CancellationToken ct)
    {
        var storedFamily = await db.SessionFamilies.SingleOrDefaultAsync(f => f.Id == family && f.UserId == user.Id, ct);
        if (storedFamily is null)
        {
            db.SessionFamilies.Add(new SessionFamily { Id = family, UserId = user.Id, SecurityStamp = user.SecurityStamp ?? "" });
        }
        else if (storedFamily.RevokedAt.HasValue || storedFamily.SecurityStamp != user.SecurityStamp)
        {
            throw new RuleException(ErrorKind.Unauthorized, "Your session expired. Please sign in.");
        }
        var now = DateTimeOffset.UtcNow;
        var accessExpiry = now.AddMinutes(15);
        var refreshExpiry = now.AddDays(7);
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        db.RefreshSessions.Add(new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = family,
            TokenHash = Hash(raw),
            ExpiresAt = refreshExpiry
        });
        await db.SaveChangesAsync(ct);
        var view = await ViewAsync(user);
        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()), new Claim("name", user.DisplayName),
            new Claim("role", view.Role), new Claim("sid", family.ToString())
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime, accessExpiry.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new AuthGrant(view, new JwtSecurityTokenHandler().WriteToken(token), raw, accessExpiry, refreshExpiry);
    }

    private async Task<UserView> ViewAsync(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var role = roles.Contains("Admin") ? "Admin" : roles.Contains("Agent") ? "Agent" : "Customer";
        return new UserView(user.Id, user.Email!, user.DisplayName, role);
    }

    private async Task<int> RevokeFamilyAsync(Guid userId, Guid family, CancellationToken ct)
    {
        await db.SessionFamilies.Where(f => f.UserId == userId && f.Id == family)
            .ExecuteUpdateAsync(updates => updates.SetProperty(f => f.RevokedAt, DateTimeOffset.UtcNow), ct);
        return await db.RefreshSessions.Where(s => s.UserId == userId && s.FamilyId == family && s.RevokedAt == null)
            .ExecuteUpdateAsync(updates => updates.SetProperty(s => s.RevokedAt, DateTimeOffset.UtcNow), ct);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Email(string? value)
    {
        var email = value?.Trim() ?? "";
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            throw new RuleException(ErrorKind.Validation, "Enter a valid email address.");
        return email;
    }
}
