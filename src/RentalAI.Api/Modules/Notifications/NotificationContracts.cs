namespace RentalAI.Api.Modules.Notifications;

public sealed record NotificationResponse(Guid Id, string Title, string Message, bool IsRead, DateTime CreatedAt);

public sealed record UnreadCountResponse(int Count);

public sealed record EmailContent(string Subject, string Body);
