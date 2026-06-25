using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RentalAI.Common.Web;

namespace RentalAI.Api.Modules.Dashboard;

public static class DashboardModule
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection services)
    {
        services.AddScoped<DashboardService>();
        return services;
    }

    public static IEndpointRouteBuilder MapDashboardModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/dashboard")
            .RequireAuthorization(new AuthorizeAttribute { Roles = "Owner" });

        group.MapGet("", SummaryAsync);
        group.MapGet("/summary", SummaryAsync);
        group.MapGet("/export", ExportAsync);

        return endpoints;
    }

    private static async Task<IResult> SummaryAsync(DateOnly? from, DateOnly? to, ClaimsPrincipal user, DashboardService service, CancellationToken cancellationToken)
    {
        var ownerId = user.GetUserId();
        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = from ?? today.AddDays(-30);
        var toDate = to ?? today;

        var summary = await service.GetSummaryAsync(ownerId.Value, fromDate, toDate, cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> ExportAsync(Guid? propertyId, DateOnly? from, DateOnly? to, ClaimsPrincipal user, DashboardService service, CancellationToken cancellationToken)
    {
        var ownerId = user.GetUserId();
        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var bytes = await service.ExportAsync(ownerId.Value, propertyId, from, to, cancellationToken);
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bookings.xlsx");
    }
}
