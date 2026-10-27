namespace TicketFlow.Application.Auth;

public sealed record RegisterRequest(string Email, string Password, string Name);
public sealed record LoginRequest(string Email, string Password);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string Password);
public sealed record UserView(Guid Id, string Email, string Name, string Role);
public sealed record AuthGrant(UserView User, string AccessToken, string RefreshToken, DateTimeOffset AccessExpires, DateTimeOffset RefreshExpires);
public interface IIdentityService
{
    Task<AuthGrant> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthGrant> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthGrant> RefreshAsync(string refreshToken, CancellationToken ct);
    Task LogoutAsync(string? refreshToken, Guid? userId, Guid? familyId, CancellationToken ct);
    Task<UserView?> FindUserAsync(Guid userId, CancellationToken ct);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
}
public interface IPasswordResetSender
{
    Task SendAsync(string email, string token, CancellationToken ct);
}
