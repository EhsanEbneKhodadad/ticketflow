using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Auth;

namespace TicketFlow.Infrastructure.Identity;

public sealed class SmtpPasswordResetSender(IConfiguration config, ILogger<SmtpPasswordResetSender> logger) : IPasswordResetSender
{
    public async Task SendAsync(string email, string token, CancellationToken ct)
    {
        var origin = (config["App:PublicUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var link = origin + "/reset-password?email=" + Uri.EscapeDataString(email) + "&token=" + Uri.EscapeDataString(token);
        using var message = new MailMessage(config["Mail:From"] ?? "support@ticketflow.local", email)
        {
            Subject = "Reset your TicketFlow password",
            Body = "Use this link to reset your password. It expires in one hour and can be used once." + Environment.NewLine + Environment.NewLine + link
        };
        using var smtp = new SmtpClient(config["Mail:Host"] ?? "localhost", config.GetValue("Mail:Port", 1025));
        try { await smtp.SendMailAsync(message, ct); }
        catch (SmtpException) { logger.LogError("A password reset email could not be delivered. Check the SMTP service."); }
    }
}
