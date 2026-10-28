using Microsoft.Extensions.Primitives;
using TicketFlow.Application.Search;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Api.Security;

public static class TicketQueryNormalization
{
    public static void UseTicketQueryNormalization(this WebApplication app) => app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value?.TrimEnd('/');
        var search = string.Equals(path, "/api/v1/search/tickets", StringComparison.OrdinalIgnoreCase);
        var list = string.Equals(path, "/api/v1/tickets", StringComparison.OrdinalIgnoreCase);
        if (HttpMethods.IsGet(context.Request.Method) && (search || list))
        {
            var values = new Dictionary<string, StringValues>(context.Request.Query, StringComparer.OrdinalIgnoreCase);
            Normalize<TicketStatus>(values, "status");
            Normalize<TicketPriority>(values, "priority");
            if (search) Normalize<SearchSort>(values, "sort");
            context.Request.Query = new QueryCollection(values);
        }
        await next(context);
    });

    private static void Normalize<T>(IDictionary<string, StringValues> values, string key) where T : struct, Enum
    {
        if (!values.TryGetValue(key, out var raw)) return;
        if (raw.Count != 1 || !Enum.TryParse<T>(raw[0], ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed)
            || !string.Equals(raw[0], parsed.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new RuleException(ErrorKind.Validation, "Choose a single valid " + key + " filter.");
        values[key] = parsed.ToString();
    }
}
