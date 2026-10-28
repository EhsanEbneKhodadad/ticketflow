using TicketFlow.Domain.Common;
using System.Text.RegularExpressions;

namespace TicketFlow.Domain.Tickets;

public enum TicketStatus { Open, InProgress, WaitingForCustomer, Resolved, Closed }
public enum TicketPriority { Low, Medium, High, Urgent }
public sealed record Actor(Guid Id, string Name, string Role)
{
    public bool IsStaff => Role is "Agent" or "Admin";
}

public sealed class Ticket
{
    private readonly List<TicketMessage> _messages = [];
    private readonly List<TicketAudit> _audit = [];
    private readonly List<TicketAttachment> _attachments = [];
    public Guid Id { get; private set; }
    public long Number { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public Guid RequesterId { get; private set; }
    public string RequesterName { get; private set; } = "";
    public Guid CategoryId { get; private set; }
    public string[] Tags { get; private set; } = [];
    public Guid? AgentId { get; private set; }
    public string? AgentName { get; private set; }
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public IReadOnlyList<TicketMessage> Messages => _messages;
    public IReadOnlyList<TicketAudit> Audit => _audit;
    public IReadOnlyList<TicketAttachment> Attachments => _attachments;

    private Ticket() { }

    public static Ticket Create(Actor actor, string title, string description, Guid category, string[] tags, TicketPriority priority)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            RequesterId = actor.Id,
            RequesterName = actor.Name,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Version = 1
        };
        ticket.SetDetails(title, description, category, tags);
        CheckPriority(priority);
        ticket.Priority = priority;
        ticket.Record(actor, "created", "Ticket created");
        return ticket;
    }

    public void ChangeStatus(Actor actor, TicketStatus status, int version)
    {
        Guard(actor, version);
        if (!actor.IsStaff && !((status == TicketStatus.Closed && Status == TicketStatus.Resolved)
            || (status == TicketStatus.Open && Status is TicketStatus.Resolved or TicketStatus.Closed)))
            throw new RuleException(ErrorKind.Forbidden, "Customers may close resolved tickets or reopen their tickets.");

        var allowed = (Status, status) switch
        {
            (TicketStatus.Open, TicketStatus.InProgress or TicketStatus.Resolved) => true,
            (TicketStatus.InProgress, TicketStatus.WaitingForCustomer or TicketStatus.Resolved) => true,
            (TicketStatus.WaitingForCustomer, TicketStatus.InProgress or TicketStatus.Resolved) => true,
            (TicketStatus.Resolved, TicketStatus.Closed or TicketStatus.Open) => true,
            (TicketStatus.Closed, TicketStatus.Open) => true,
            _ => false
        };
        if (!allowed) throw new RuleException(ErrorKind.Conflict, "This status transition is not allowed.");
        var previous = Status;
        Status = status;
        Changed(actor, "statusChanged", $"Status changed from {previous} to {status}");
    }

    public void ChangePriority(Actor actor, TicketPriority priority, int version)
    {
        Staff(actor);
        Guard(actor, version);
        CheckPriority(priority);
        Priority = priority;
        Changed(actor, "priorityChanged", $"Priority set to {priority}");
    }

    public void Assign(Actor actor, Guid? id, string? name, int version)
    {
        Staff(actor);
        Guard(actor, version);
        AgentId = id;
        AgentName = id.HasValue ? name : null;
        Changed(actor, "assigned", id.HasValue ? $"Assigned to {name}" : "Assignment removed");
    }

    public void Edit(Actor actor, string title, string description, Guid category, string[] tags, int version)
    {
        Guard(actor, version);
        if (Status is TicketStatus.Resolved or TicketStatus.Closed)
            throw new RuleException(ErrorKind.Conflict, "Reopen the ticket before editing.");
        SetDetails(title, description, category, tags);
        Changed(actor, "edited", "Ticket details updated");
    }

    public void Reply(Actor actor, string body, bool internalNote, int version)
    {
        Guard(actor, version);
        if (internalNote) Staff(actor);
        if (Status == TicketStatus.Closed)
            throw new RuleException(ErrorKind.Conflict, "Reopen the ticket before replying.");
        body = Text(body, 1, 10000, "Reply");
        _messages.Add(new TicketMessage(Guid.NewGuid(), Id, actor.Id, actor.Name, actor.Role, body, internalNote, DateTimeOffset.UtcNow));
        Changed(actor, internalNote ? "internalNoteAdded" : "replyAdded", internalNote ? "Internal note added" : "Public reply added", internalNote);
    }

    public TicketAttachment Attach(Actor actor, string fileName, string contentType, long size, string objectKey, Guid? messageId, int version)
    {
        Guard(actor, version);
        if (Status == TicketStatus.Closed)
            throw new RuleException(ErrorKind.Conflict, "Reopen the ticket before uploading attachments.");
        if (_attachments.Count >= 10)
            throw new RuleException(ErrorKind.Conflict, "A ticket can contain up to ten attachments.");
        var message = messageId.HasValue ? _messages.SingleOrDefault(m => m.Id == messageId) : null;
        if (messageId.HasValue && message is null)
            throw new RuleException(ErrorKind.Validation, "Choose a reply on this ticket.");
        if (message?.Internal == true && !actor.IsStaff)
            throw new RuleException(ErrorKind.NotFound, "Reply not found.");
        if (size is <= 0 or > 10 * 1024 * 1024)
            throw new RuleException(ErrorKind.Validation, "Files must contain 1 byte to 10 MiB.");
        var attachment = new TicketAttachment(Guid.NewGuid(), Id, messageId, fileName, contentType, size,
            objectKey, message?.Internal == true, actor.Id, DateTimeOffset.UtcNow);
        _attachments.Add(attachment);
        Changed(actor, "attachmentAdded", "Attachment added", attachment.Internal);
        return attachment;
    }

    private void SetDetails(string title, string description, Guid category, string[] tags)
    {
        Title = Text(title, 5, 160, "Title");
        Description = Text(description, 10, 10000, "Description");
        if (category == Guid.Empty) throw new RuleException(ErrorKind.Validation, "Choose a category.");
        CategoryId = category;
        var normalized = (tags ?? []).Select(tag => (tag ?? "").Trim().ToLowerInvariant()).Distinct().ToArray();
        if (normalized.Length > 5 || normalized.Any(tag => !Regex.IsMatch(tag, "^[a-z0-9][a-z0-9-]{0,23}$")))
            throw new RuleException(ErrorKind.Validation, "Use up to five tags containing letters, numbers or hyphens (24 characters each).");
        Tags = normalized;
    }

    private static string Text(string? value, int min, int max, string label)
    {
        value = value?.Trim() ?? "";
        if (value.Length < min || value.Length > max)
            throw new RuleException(ErrorKind.Validation, $"{label} must contain {min} to {max} characters.");
        return value;
    }

    private static void CheckPriority(TicketPriority value)
    {
        if (!Enum.IsDefined(value)) throw new RuleException(ErrorKind.Validation, "Unknown priority.");
    }

    private void Guard(Actor actor, int version)
    {
        if (!actor.IsStaff && actor.Id != RequesterId)
            throw new RuleException(ErrorKind.Forbidden, "You cannot change this ticket.");
        if (version != Version)
            throw new RuleException(ErrorKind.Conflict, "This ticket changed. Refresh it before trying again.");
    }

    private static void Staff(Actor actor)
    {
        if (!actor.IsStaff) throw new RuleException(ErrorKind.Forbidden, "This action requires a support role.");
    }

    private void Changed(Actor actor, string kind, string summary, bool internalEvent = false)
    {
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
        Record(actor, kind, summary, internalEvent);
    }

    private void Record(Actor actor, string kind, string summary, bool internalEvent = false) =>
        _audit.Add(new TicketAudit(Guid.NewGuid(), Id, actor.Id, actor.Name, kind, summary, internalEvent, DateTimeOffset.UtcNow));
}

public sealed record TicketMessage(Guid Id, Guid TicketId, Guid AuthorId, string AuthorName, string AuthorRole, string Body, bool Internal, DateTimeOffset CreatedAt);
public sealed record TicketAudit(Guid Id, Guid TicketId, Guid ActorId, string ActorName, string Kind, string Summary, bool Internal, DateTimeOffset CreatedAt);
