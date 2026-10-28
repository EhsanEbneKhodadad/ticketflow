namespace TicketFlow.Api.Security;

public static class ExpectedUserPrecondition
{
    public static void UseExpectedUserPrecondition(this WebApplication app) => app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/v1")
            && context.Request.Headers.TryGetValue("X-TicketFlow-User", out var values))
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (values.Count != 1 || !Guid.TryParse(values[0], out var expectedUser))
            {
                await Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid initiating account.",
                    detail: "Provide one valid account ID in X-TicketFlow-User.").ExecuteAsync(context);
                return;
            }
            if (RequestIdentity.Actor(context).Id != expectedUser)
            {
                await Results.Problem(statusCode: StatusCodes.Status412PreconditionFailed,
                    title: "Account changed.",
                    detail: "Your account changed in another tab. Sign in to this workspace again.",
                    extensions: new Dictionary<string, object?> { ["code"] = "account_changed" }).ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    });
}
