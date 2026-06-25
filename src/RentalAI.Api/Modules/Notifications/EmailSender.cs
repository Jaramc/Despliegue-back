using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace RentalAI.Api.Modules.Notifications;

public sealed class EmailSender(MailOptions options, ILogger<EmailSender> logger)
{
    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(options.FromName, options.FromAddress));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            var secureOptions = options.Encryption.Equals("tls", StringComparison.OrdinalIgnoreCase)
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            using var client = new SmtpClient();
            await client.ConnectAsync(options.Host, options.Port, secureOptions, cancellationToken);
            await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("Email sent to {Recipient} with subject {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Recipient}", toEmail);
        }
    }
}
