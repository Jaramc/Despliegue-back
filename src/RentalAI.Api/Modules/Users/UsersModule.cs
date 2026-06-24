using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RentalAI.Common.Web;

namespace RentalAI.Api.Modules.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services)
    {
        services.AddScoped<WishlistService>();
        services.AddSingleton<WishlistCookie>();
        return services;
    }

    public static IEndpointRouteBuilder MapUsersModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users");

        group.MapPost("/wishlist/{propertyId:guid}", AddToWishlistAsync);
        group.MapDelete("/wishlist/{propertyId:guid}", RemoveFromWishlistAsync);
        group.MapGet("/wishlist", GetWishlistAsync);
        group.MapGet("/profile", GetProfileAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> AddToWishlistAsync(Guid propertyId, HttpContext http, ClaimsPrincipal user, WishlistService service, WishlistCookie cookie, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is not null)
        {
            var added = await service.AddAsync(userId.Value, propertyId, cancellationToken);
            return added ? Results.NoContent() : Results.NotFound();
        }

        var ids = cookie.Read(http.Request).ToList();
        if (!ids.Contains(propertyId))
        {
            ids.Add(propertyId);
        }

        cookie.Write(http.Response, ids);
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveFromWishlistAsync(Guid propertyId, HttpContext http, ClaimsPrincipal user, WishlistService service, WishlistCookie cookie, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is not null)
        {
            await service.RemoveAsync(userId.Value, propertyId, cancellationToken);
            return Results.NoContent();
        }

        var ids = cookie.Read(http.Request).Where(id => id != propertyId).ToList();
        cookie.Write(http.Response, ids);
        return Results.NoContent();
    }

    private static async Task<IResult> GetWishlistAsync(HttpContext http, ClaimsPrincipal user, WishlistService service, WishlistCookie cookie, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        var items = userId is not null
            ? await service.GetForUserAsync(userId.Value, cancellationToken)
            : await service.GetSummariesAsync(cookie.Read(http.Request), cancellationToken);

        return Results.Ok(items);
    }

    private static async Task<IResult> GetProfileAsync(ClaimsPrincipal user, WishlistService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var profile = await service.GetProfileAsync(userId.Value, cancellationToken);
        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }
}
