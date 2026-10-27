using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TicketFlow.Infrastructure.Identity;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Api.Security;

public static class SecurityConfiguration
{
    public static void AddTicketFlowSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        builder.Services.AddAuthorization();
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<AuthSettings>((options, settings) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(15),
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["ticketflow_access"];
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var user)
                            || !Guid.TryParse(context.Principal?.FindFirst("sid")?.Value, out var family))
                        {
                            context.Fail("Invalid session.");
                            return;
                        }
                        var db = context.HttpContext.RequestServices.GetRequiredService<TicketFlowDbContext>();
                        var active = await db.SessionFamilies.AsNoTracking().AnyAsync(f => f.Id == family && f.UserId == user
                            && f.RevokedAt == null
                            && db.Users.Any(u => u.Id == user && u.SecurityStamp == f.SecurityStamp)
                            && db.RefreshSessions.Any(s => s.UserId == user && s.FamilyId == family && s.RevokedAt == null && s.ExpiresAt > DateTimeOffset.UtcNow),
                            context.HttpContext.RequestAborted);
                        if (!active) context.Fail("Session revoked.");
                    }
                };
            });
        var local = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "ticketflow_csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = local ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = local ? 100 : 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
    }
}
