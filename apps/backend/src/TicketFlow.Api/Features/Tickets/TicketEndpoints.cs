using TicketFlow.Api.Security;
using TicketFlow.Application.Tickets;

namespace TicketFlow.Api.Features.Tickets;

public static class TicketEndpoints
{
    public static void MapTickets(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization().WithTags("Tickets");
        api.MapGet("/tickets", async ([AsParameters] TicketQuery query, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.ListAsync(RequestIdentity.Actor(context), query, ct)));
        api.MapGet("/tickets/{id:guid}", async (Guid id, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.GetAsync(RequestIdentity.Actor(context), id, ct)));
        api.MapPost("/tickets", async (CreateTicketRequest input, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
        {
            var ticket = await workflow.CreateAsync(RequestIdentity.Actor(context), input, ct);
            return TypedResults.Created("/api/v1/tickets/" + ticket.Id, ticket);
        });
        api.MapPut("/tickets/{id:guid}", async (Guid id, EditTicketRequest input, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.EditAsync(RequestIdentity.Actor(context), id, input, ct)));
        api.MapPost("/tickets/{id:guid}/messages", async (Guid id, MessageRequest input, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.ReplyAsync(RequestIdentity.Actor(context), id, input, ct)));
        api.MapPatch("/tickets/{id:guid}/status", async (Guid id, StatusRequest input, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.StatusAsync(RequestIdentity.Actor(context), id, input, ct)));
        api.MapPatch("/tickets/{id:guid}/priority", async (Guid id, PriorityRequest input, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.PriorityAsync(RequestIdentity.Actor(context), id, input, ct)));
        api.MapPatch("/tickets/{id:guid}/assignment", async (Guid id, AssignmentRequest input, TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.AssignAsync(RequestIdentity.Actor(context), id, input, ct)));
        api.MapGet("/categories", async (TicketWorkflow workflow, CancellationToken ct) => TypedResults.Ok(await workflow.CategoriesAsync(ct)));
        api.MapGet("/agents", async (TicketWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.AgentsAsync(RequestIdentity.Actor(context), ct))).WithTags("Administration");
    }
}
