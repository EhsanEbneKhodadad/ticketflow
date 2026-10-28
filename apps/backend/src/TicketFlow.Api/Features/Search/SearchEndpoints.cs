using TicketFlow.Api.Security;
using TicketFlow.Application.Search;

namespace TicketFlow.Api.Features.Search;

public static class SearchEndpoints
{
    public static void MapSearch(this WebApplication app)
    {
        app.MapGet("/api/v1/search/tickets", async ([AsParameters] TicketSearchQuery query, TicketSearchWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.SearchAsync(RequestIdentity.Actor(context), query, ct)))
            .RequireAuthorization().WithTags("Search")
            .WithSummary("Search public ticket content within the first 1000 candidates.")
            .WithDescription("from/to are inclusive UTC creation dates in YYYY-MM-DD format. Snippet segments are plain text; render highlighted segments as text within mark elements.")
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}
