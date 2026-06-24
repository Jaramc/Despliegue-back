using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RentalAI.Common.Web;
using StackExchange.Redis;

namespace RentalAI.Api.Modules.Booking;

public static class BookingModule
{
    public static IServiceCollection AddBookingModule(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new ConfigurationOptions
        {
            EndPoints = { { configuration["REDIS_HOST"] ?? "localhost", int.Parse(configuration["REDIS_PORT"] ?? "6379") } },
            Password = configuration["REDIS_PASSWORD"],
            AbortOnConnectFail = false
        };

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddScoped<BookingService>();
        services.AddScoped<IValidator<CreateBookingRequest>, CreateBookingRequestValidator>();
        return services;
    }

    public static IEndpointRouteBuilder MapBookingModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bookings").RequireAuthorization();

        group.MapPost("", CreateAsync).AddEndpointFilter<ValidationFilter<CreateBookingRequest>>();
        group.MapGet("", GetMineAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapPost("/{id:guid}/cancel", CancelAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreateBookingRequest request, ClaimsPrincipal user, BookingService service, CancellationToken cancellationToken)
    {
        var guestId = user.GetUserId();
        if (guestId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await service.CreateAsync(guestId.Value, request, cancellationToken);
        return outcome.Error switch
        {
            BookingError.None => Results.Created($"/bookings/{outcome.Booking!.Id}", outcome.Booking),
            BookingError.KycRequired => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "KYC approval is required before booking"),
            BookingError.PropertyNotFound => Results.NotFound(),
            BookingError.InvalidDates => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid booking dates"),
            BookingError.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The property is not available for those dates"),
            _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request")
        };
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal user, BookingService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var bookings = await service.GetMyBookingsAsync(userId.Value, cancellationToken);
        return Results.Ok(bookings);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, ClaimsPrincipal user, BookingService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await service.GetByIdAsync(userId.Value, id, cancellationToken);
        return outcome.Error switch
        {
            BookingError.None => Results.Ok(outcome.Booking),
            BookingError.Forbidden => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "You cannot view this booking"),
            _ => Results.NotFound()
        };
    }

    private static async Task<IResult> CancelAsync(Guid id, ClaimsPrincipal user, BookingService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await service.CancelAsync(userId.Value, id, cancellationToken);
        return outcome.Error switch
        {
            BookingError.None => Results.Ok(outcome.Booking),
            BookingError.Forbidden => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "You cannot cancel this booking"),
            BookingError.InvalidState => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The booking cannot be cancelled in its current state"),
            _ => Results.NotFound()
        };
    }
}
