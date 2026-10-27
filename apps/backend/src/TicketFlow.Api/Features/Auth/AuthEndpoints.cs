using Microsoft.AspNetCore.Antiforgery;
using TicketFlow.Api.Security;
using TicketFlow.Application.Auth;

namespace TicketFlow.Api.Features.Auth;

public sealed record CsrfView(string Token);
public sealed record StatusMessage(string Message);

public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        var auth = app.MapGroup("/api/v1/auth").WithTags("Authentication");
        auth.MapGet("/csrf", (IAntiforgery antiforgery, HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return TypedResults.Ok(new CsrfView(antiforgery.GetAndStoreTokens(context).RequestToken!));
        }).AllowAnonymous();

        auth.MapPost("/register", async (RegisterRequest input, IIdentityService identity, HttpContext context, IHostEnvironment environment, CancellationToken ct) =>
        {
            var grant = await identity.RegisterAsync(input, ct);
            Cookies(context, grant, environment);
            return TypedResults.Created("/api/v1/auth/me", grant.User);
        }).AllowAnonymous().RequireRateLimiting("auth");

        auth.MapPost("/login", async (LoginRequest input, IIdentityService identity, HttpContext context, IHostEnvironment environment, CancellationToken ct) =>
        {
            var grant = await identity.LoginAsync(input, ct);
            Cookies(context, grant, environment);
            return TypedResults.Ok(grant.User);
        }).AllowAnonymous().RequireRateLimiting("auth");

        auth.MapGet("/me", async (IIdentityService identity, HttpContext context, CancellationToken ct) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var user = await identity.FindUserAsync(RequestIdentity.Actor(context).Id, ct);
            return user is null ? Results.Unauthorized() : Results.Ok(user);
        }).Produces<UserView>().RequireAuthorization();

        auth.MapPost("/refresh", async (IIdentityService identity, HttpContext context, IHostEnvironment environment, CancellationToken ct) =>
        {
            var grant = await identity.RefreshAsync(context.Request.Cookies["ticketflow_refresh"] ?? "", ct);
            Cookies(context, grant, environment);
            return TypedResults.Ok(grant.User);
        }).AllowAnonymous().RequireRateLimiting("auth");

        auth.MapPost("/logout", async (IIdentityService identity, HttpContext context, CancellationToken ct) =>
        {
            await identity.LogoutAsync(context.Request.Cookies["ticketflow_refresh"],
                RequestIdentity.GuidClaim(context, "sub"), RequestIdentity.GuidClaim(context, "sid"), ct);
            ClearCookies(context);
            return TypedResults.NoContent();
        }).AllowAnonymous();

        auth.MapPost("/forgot-password", async (ForgotPasswordRequest input, IIdentityService identity, CancellationToken ct) =>
        {
            await identity.ForgotPasswordAsync(input, ct);
            return TypedResults.Accepted((string?)null, new StatusMessage("If an account exists, a reset link has been sent."));
        }).AllowAnonymous().RequireRateLimiting("auth");

        auth.MapPost("/reset-password", async (ResetPasswordRequest input, IIdentityService identity, HttpContext context, CancellationToken ct) =>
        {
            await identity.ResetPasswordAsync(input, ct);
            ClearCookies(context);
            return TypedResults.NoContent();
        }).AllowAnonymous().RequireRateLimiting("auth");
    }

    private static void Cookies(HttpContext context, AuthGrant grant, IHostEnvironment environment)
    {
        var secure = !environment.IsDevelopment() && !environment.IsEnvironment("Testing");
        context.Response.Cookies.Append("ticketflow_access", grant.AccessToken,
            new CookieOptions { HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Lax, Path = "/", Expires = grant.AccessExpires });
        context.Response.Cookies.Append("ticketflow_refresh", grant.RefreshToken,
            new CookieOptions { HttpOnly = true, Secure = secure, SameSite = SameSiteMode.Lax, Path = "/api/v1/auth", Expires = grant.RefreshExpires });
        context.Response.Headers.CacheControl = "no-store";
    }

    private static void ClearCookies(HttpContext context)
    {
        context.Response.Cookies.Delete("ticketflow_access", new CookieOptions { Path = "/" });
        context.Response.Cookies.Delete("ticketflow_refresh", new CookieOptions { Path = "/api/v1/auth" });
    }
}
