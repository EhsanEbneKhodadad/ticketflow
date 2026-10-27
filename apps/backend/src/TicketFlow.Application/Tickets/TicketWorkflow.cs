using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Tickets;

public sealed class TicketWorkflow(ITicketStore store)
{
    public Task<TicketPage> ListAsync(Actor actor, TicketQuery query, CancellationToken ct)
    {
        if (query.Page is < 1 or > 10000 || query.PageSize is < 1 or > 100 || query.Q?.Length > 160)
            throw new RuleException(ErrorKind.Validation, "Use a positive page, page size up to 100, and a query up to 160 characters.");
        return store.ListAsync(actor, query, ct);
    }

    public async Task<TicketView> GetAsync(Actor actor, Guid id, CancellationToken ct) =>
        await ViewAsync(await FindAsync(actor, id, ct), actor, ct);

    public async Task<TicketView> CreateAsync(Actor actor, CreateTicketRequest input, CancellationToken ct)
    {
        await CategoryAsync(input.CategoryId, ct);
        var ticket = Ticket.Create(actor, input.Title, input.Description, input.CategoryId, input.Tags ?? [], input.Priority);
        await store.AddAsync(ticket, ct);
        await store.SaveAsync(ct);
        return await ViewAsync(ticket, actor, ct);
    }

    public async Task<TicketView> EditAsync(Actor actor, Guid id, EditTicketRequest input, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        await CategoryAsync(input.CategoryId, ct);
        ticket.Edit(actor, input.Title, input.Description, input.CategoryId, input.Tags ?? [], input.ExpectedVersion);
        return await SaveViewAsync(ticket, actor, ct);
    }

    public async Task<TicketView> ReplyAsync(Actor actor, Guid id, MessageRequest input, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        ticket.Reply(actor, input.Body, input.Internal, input.ExpectedVersion);
        return await SaveViewAsync(ticket, actor, ct);
    }

    public async Task<TicketView> StatusAsync(Actor actor, Guid id, StatusRequest input, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        ticket.ChangeStatus(actor, input.Status, input.ExpectedVersion);
        return await SaveViewAsync(ticket, actor, ct);
    }

    public async Task<TicketView> PriorityAsync(Actor actor, Guid id, PriorityRequest input, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        ticket.ChangePriority(actor, input.Priority, input.ExpectedVersion);
        return await SaveViewAsync(ticket, actor, ct);
    }

    public async Task<TicketView> AssignAsync(Actor actor, Guid id, AssignmentRequest input, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        Staff(actor);
        var agents = await store.AgentsAsync(ct);
        var agent = input.AgentId.HasValue ? agents.FirstOrDefault(a => a.Id == input.AgentId) : null;
        if (input.AgentId.HasValue && agent is null) throw new RuleException(ErrorKind.Validation, "Choose an active support agent.");
        ticket.Assign(actor, agent?.Id, agent?.Name, input.ExpectedVersion);
        return await SaveViewAsync(ticket, actor, ct);
    }

    public Task<IReadOnlyList<CategoryView>> CategoriesAsync(CancellationToken ct) => store.CategoriesAsync(ct);

    public Task<IReadOnlyList<AgentView>> AgentsAsync(Actor actor, CancellationToken ct)
    {
        Staff(actor);
        return store.AgentsAsync(ct);
    }

    private async Task<Ticket> FindAsync(Actor actor, Guid id, CancellationToken ct)
    {
        var ticket = await store.FindAsync(id, ct);
        if (ticket is null || (!actor.IsStaff && ticket.RequesterId != actor.Id))
            throw new RuleException(ErrorKind.NotFound, "Ticket not found.");
        return ticket;
    }

    private async Task CategoryAsync(Guid id, CancellationToken ct)
    {
        if (!(await store.CategoriesAsync(ct)).Any(c => c.Id == id))
            throw new RuleException(ErrorKind.Validation, "Choose an existing category.");
    }

    private async Task<TicketView> SaveViewAsync(Ticket ticket, Actor actor, CancellationToken ct)
    {
        await store.SaveAsync(ct);
        return await ViewAsync(ticket, actor, ct);
    }

    private async Task<TicketView> ViewAsync(Ticket ticket, Actor actor, CancellationToken ct)
    {
        var category = (await store.CategoriesAsync(ct)).First(c => c.Id == ticket.CategoryId).Name;
        return new TicketView(ticket.Id, $"TKT-{ticket.Number}", ticket.Title, ticket.Description, ticket.RequesterId, ticket.RequesterName,
            ticket.CategoryId, category, ticket.Tags, ticket.Status, ticket.Priority, ticket.AgentId, ticket.AgentName,
            ticket.Version, ticket.CreatedAt, ticket.UpdatedAt,
            ticket.Messages.Where(m => actor.IsStaff || !m.Internal).OrderBy(m => m.CreatedAt)
                .Select(m => new MessageView(m.Id, m.AuthorId, m.AuthorName, m.AuthorRole, m.Body, m.Internal, m.CreatedAt)).ToArray(),
            ticket.Audit.Where(e => actor.IsStaff || !e.Internal).OrderBy(e => e.CreatedAt)
                .Select(e => new AuditView(e.Id, e.ActorName, e.Kind, e.Summary, e.CreatedAt)).ToArray());
    }

    private static void Staff(Actor actor)
    {
        if (!actor.IsStaff) throw new RuleException(ErrorKind.Forbidden, "This action requires a support role.");
    }
}
