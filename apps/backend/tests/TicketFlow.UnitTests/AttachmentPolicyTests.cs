using System.Text;
using TicketFlow.Application.Attachments;
using TicketFlow.Domain.Tickets;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class AttachmentPolicyTests
{
    [Theory]
    [InlineData("../folder/diagnostic.txt", "diagnostic.txt")]
    [InlineData("C:\\temp\\diagnostic.txt", "diagnostic.txt")]
    [InlineData("diag\r\n\"<evil>.txt", "diagevil.txt")]
    public void Display_names_remove_paths_and_header_unsafe_characters(string input, string expected)
    {
        var result = AttachmentPolicy.Validate(input, Encoding.UTF8.GetBytes("Valid UTF-8 details \u2603"));
        Assert.Equal(expected, result.FileName);
        Assert.Equal("text/plain", result.ContentType);
    }

    [Theory]
    [InlineData("fake.pdf")]
    [InlineData("fake.png")]
    [InlineData("fake.jpg")]
    [InlineData("code.exe")]
    [InlineData("../../")]
    public void Extensions_and_bytes_must_agree(string name) =>
        Assert.Equal(ErrorKind.Validation, Assert.Throws<RuleException>(() => AttachmentPolicy.Validate(name, "text"u8.ToArray())).Kind);

    [Fact]
    public void Empty_oversize_binary_and_invalid_utf8_text_are_rejected()
    {
        foreach (var bytes in new[] { Array.Empty<byte>(), new byte[AttachmentPolicy.MaxFileBytes + 1], new byte[] { 0 }, new byte[] { 0xC3, 0x28 } })
            Assert.Equal(ErrorKind.Validation, Assert.Throws<RuleException>(() => AttachmentPolicy.Validate("text.txt", bytes)).Kind);
        var maximum = Enumerable.Repeat((byte)'x', AttachmentPolicy.MaxFileBytes).ToArray();
        Assert.Equal("text/plain", AttachmentPolicy.Validate("maximum.txt", maximum).ContentType);
    }

    [Fact]
    public void Supported_file_signatures_choose_server_controlled_content_types()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jf9sAAAAASUVORK5CYII=");
        Assert.Equal("image/png", AttachmentPolicy.Validate("pixel.PNG", png).ContentType);
        Assert.Equal("application/pdf", AttachmentPolicy.Validate("document.pdf", "%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\n%%EOF"u8.ToArray()).ContentType);
        Assert.Equal("image/jpeg", AttachmentPolicy.Validate("image.jpeg", Convert.FromBase64String("/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD3+iiigD//2Q==")).ContentType);
    }

    [Fact]
    public void Aggregate_derives_reply_privacy_and_rejects_closed_stale_and_foreign_uploads()
    {
        var owner = new Actor(Guid.NewGuid(), "Owner", "Customer");
        var staff = new Actor(Guid.NewGuid(), "Agent", "Agent");
        var ticket = Ticket.Create(owner, "Attachment issue", "Please inspect the attachment.", Guid.NewGuid(), [], TicketPriority.Low);
        ticket.Reply(staff, "Private diagnostic", true, 1);
        var message = Assert.Single(ticket.Messages);
        Assert.Equal(ErrorKind.NotFound, Assert.Throws<RuleException>(() => ticket.Attach(owner, "private.txt", "text/plain", 1, "key", message.Id, 2)).Kind);
        var attachment = ticket.Attach(staff, "private.txt", "text/plain", 1, "key", message.Id, 2);
        Assert.True(attachment.Internal);
        Assert.True(ticket.Audit.Last().Internal);
        Assert.Equal(3, ticket.Version);
        Assert.Equal(ErrorKind.Conflict, Assert.Throws<RuleException>(() => ticket.Attach(owner, "file.txt", "text/plain", 1, "key2", null, 2)).Kind);
        Assert.Equal(ErrorKind.Forbidden, Assert.Throws<RuleException>(() => ticket.Attach(new Actor(Guid.NewGuid(), "Other", "Customer"), "file.txt", "text/plain", 1, "key2", null, 3)).Kind);
        ticket.ChangeStatus(staff, TicketStatus.Resolved, 3);
        ticket.ChangeStatus(owner, TicketStatus.Closed, 4);
        Assert.Equal(ErrorKind.Conflict, Assert.Throws<RuleException>(() => ticket.Attach(owner, "file.txt", "text/plain", 1, "key2", null, 5)).Kind);
    }
}
