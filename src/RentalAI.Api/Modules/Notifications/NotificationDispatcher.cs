using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RentalAI.Api.Modules.Notifications;

public sealed class NotificationDispatcher(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatcher> logger)
{
    public void BookingConfirmed(Guid guestId, string guestEmail, Guid ownerId, string propertyTitle, DateTime checkIn, DateTime checkOut, decimal totalPrice) =>
        Run(async services =>
        {
            var email = services.GetRequiredService<EmailSender>();
            var notifications = services.GetRequiredService<NotificationService>();

            await notifications.CreateAsync(guestId, "Reserva confirmada", $"Tu reserva en {propertyTitle} está confirmada", CancellationToken.None);
            await notifications.CreateAsync(ownerId, "Nueva reserva", $"Nueva reserva en tu inmueble {propertyTitle}", CancellationToken.None);

            var content = NotificationTemplates.BookingConfirmed(guestEmail, propertyTitle, checkIn, checkOut, totalPrice);
            await email.SendAsync(guestEmail, content.Subject, content.Body, CancellationToken.None);
        });

    public void KycResult(Guid userId, string userEmail, bool approved, string? reason) =>
        Run(async services =>
        {
            var email = services.GetRequiredService<EmailSender>();
            var notifications = services.GetRequiredService<NotificationService>();

            if (approved)
            {
                await notifications.CreateAsync(userId, "KYC aprobado", "Tu verificación de identidad fue aprobada", CancellationToken.None);
                var content = NotificationTemplates.KycApproved(userEmail);
                await email.SendAsync(userEmail, content.Subject, content.Body, CancellationToken.None);
            }
            else
            {
                await notifications.CreateAsync(userId, "KYC rechazado", $"Tu verificación de identidad fue rechazada: {reason}", CancellationToken.None);
                var content = NotificationTemplates.KycRejected(userEmail, reason ?? string.Empty);
                await email.SendAsync(userEmail, content.Subject, content.Body, CancellationToken.None);
            }
        });

    private void Run(Func<IServiceProvider, Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await work(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification dispatch failed");
            }
        });
    }
}
