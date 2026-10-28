namespace TicketFlow.Domain.Common;

public enum ErrorKind { Validation, Unauthorized, Forbidden, NotFound, Conflict, Unavailable }
public sealed class RuleException(ErrorKind kind, string message) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;
}
