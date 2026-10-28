using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TicketFlow.Api.Errors;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        var status = error switch
        {
            RuleException { Kind: ErrorKind.Validation } => 400,
            RuleException { Kind: ErrorKind.Unauthorized } => 401,
            RuleException { Kind: ErrorKind.Forbidden } => 403,
            RuleException { Kind: ErrorKind.NotFound } => 404,
            RuleException { Kind: ErrorKind.Conflict } => 409,
            RuleException { Kind: ErrorKind.Unavailable } => 503,
            BadHttpRequestException => 400,
            InvalidDataException => 400,
            _ => 500
        };
        if (status == 500) logger.LogError(error, "An unexpected API error occurred.");
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = status == 500 ? "An unexpected error occurred." : "The request could not be completed.",
            Detail = error is RuleException ? error.Message : status == 400 ? "Check the request fields." : "Please try again later."
        }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
