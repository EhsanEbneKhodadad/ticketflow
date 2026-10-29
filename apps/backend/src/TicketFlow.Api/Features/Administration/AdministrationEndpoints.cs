using TicketFlow.Api.Security;
using TicketFlow.Application.Administration;

namespace TicketFlow.Api.Features.Administration;

public static class AdministrationEndpoints
{
    public static void MapAdministration(this WebApplication app)
    {
        app.MapGet("/api/v1/dashboard", async (DashboardWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.ReadAsync(RequestIdentity.Actor(context), ct)))
            .RequireAuthorization().WithTags("Dashboard");
        var api = app.MapGroup("/api/v1/admin").RequireAuthorization().WithTags("Administration");
        api.MapGet("/categories", async (AdministrationWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.CategoriesAsync(RequestIdentity.Actor(context), ct)));
        api.MapPost("/categories", async (CategoryRequest input, AdministrationWorkflow workflow, HttpContext context, CancellationToken ct) =>
        {
            var result = await workflow.CreateCategoryAsync(RequestIdentity.Actor(context), input, ct);
            return TypedResults.Created("/api/v1/admin/categories/" + result.Id, result);
        });
        api.MapPut("/categories/{id:guid}", async (Guid id, CategoryRequest input, AdministrationWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.UpdateCategoryAsync(RequestIdentity.Actor(context), id, input, ct)));
        api.MapGet("/users", async (AdministrationWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.UsersAsync(RequestIdentity.Actor(context), ct)));
        api.MapPatch("/users/{id:guid}/role", async (Guid id, ChangeRoleRequest input, AdministrationWorkflow workflow, HttpContext context, CancellationToken ct) =>
            TypedResults.Ok(await workflow.ChangeRoleAsync(RequestIdentity.Actor(context), id, input, ct)));
    }
}
