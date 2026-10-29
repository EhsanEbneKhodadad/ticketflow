using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace TicketFlow.Infrastructure.Notifications;

public interface INotificationSender
{
    Task SendAsync(string address, Guid ticketId, long number, string kind, Guid messageId, CancellationToken ct);
}

public sealed class SmtpNotificationSender(IConfiguration config) : INotificationSender
{
    public async Task SendAsync(string address, Guid ticketId, long number, string kind, Guid messageId, CancellationToken ct)
    {
        var origin = (config["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/');
        using var message = new MailMessage(config["Mail:From"] ?? "support@ticketflow.local", address)
        {
            Subject = $"TicketFlow update: TKT-{number}",
            Body = $"There is an update on ticket TKT-{number}. Sign in to view it: {origin}/tickets/{ticketId}"
        };
        message.Headers.Add("X-TicketFlow-Event", messageId.ToString());
        using var smtp = new SmtpClient(config["Mail:Host"] ?? "localhost", config.GetValue("Mail:Port", 1025));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await smtp.SendMailAsync(message, timeout.Token);
    }
}
