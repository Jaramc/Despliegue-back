using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;

namespace RentalAI.Api.Modules.Notifications;

public sealed class NotificationService(AppDbContext db)
{
    public async Task CreateAsync(Guid userId, string title, string message, CancellationToken cancellationToken)
    {
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationResponse(n.Id, n.Title, n.Message, n.IsRead, n.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
}
