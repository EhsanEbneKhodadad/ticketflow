using System.Security.Claims;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Api.Security;

public static class RequestIdentity
{
    public static Actor Actor(HttpContext context) => new(Guid.Parse(context.User.FindFirstValue("sub")!),
        context.User.FindFirstValue("name")!, context.User.FindFirstValue("role")!);
    public static Guid? GuidClaim(HttpContext context, string name) =>
        Guid.TryParse(context.User.FindFirstValue(name), out var value) ? value : null;
}
