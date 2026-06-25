using Microsoft.Extensions.Configuration;

namespace RentalAI.Api.Modules.Notifications;

public sealed record MailOptions(
    string Host,
    int Port,
    string Username,
    string Password,
    string FromAddress,
    string FromName,
    string Encryption)
{
    public static MailOptions FromConfiguration(IConfiguration configuration) => new(
        configuration["MAIL_HOST"] ?? "localhost",
        int.Parse(configuration["MAIL_PORT"] ?? "587"),
        configuration["MAIL_USERNAME"] ?? string.Empty,
        configuration["MAIL_PASSWORD"] ?? string.Empty,
        configuration["MAIL_FROM_ADDRESS"] ?? "noreply@rentalai.local",
        (configuration["MAIL_FROM_NAME"] ?? "RentalAI").Trim('"'),
        (configuration["MAIL_ENCRYPTION"] ?? "tls").Trim('"'));
}
