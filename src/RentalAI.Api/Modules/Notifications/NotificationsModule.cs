using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentalAI.Common.Web;

namespace RentalAI.Api.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(MailOptions.FromConfiguration(configuration));
        services.AddScoped<EmailSender>();
        services.AddScoped<NotificationService>();
        services.AddSingleton<NotificationDispatcher>();
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/notifications").RequireAuthorization();

        group.MapGet("", GetMineAsync);
        group.MapPost("/{id:guid}/read", MarkReadAsync);
        group.MapGet("/unread-count", UnreadCountAsync);

        return endpoints;
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal user, NotificationService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var notifications = await service.GetForUserAsync(userId.Value, cancellationToken);
        return Results.Ok(notifications);
    }

    private static async Task<IResult> MarkReadAsync(Guid id, ClaimsPrincipal user, NotificationService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var marked = await service.MarkReadAsync(userId.Value, id, cancellationToken);
        return marked ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> UnreadCountAsync(ClaimsPrincipal user, NotificationService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var count = await service.UnreadCountAsync(userId.Value, cancellationToken);
        return Results.Ok(new UnreadCountResponse(count));
    }
}
