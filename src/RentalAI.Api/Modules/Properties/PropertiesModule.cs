using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RentalAI.Api.Modules.Files;
using RentalAI.Common.Web;

namespace RentalAI.Api.Modules.Properties;

public static class PropertiesModule
{
    public static IServiceCollection AddPropertiesModule(this IServiceCollection services)
    {
        services.AddScoped<PropertyService>();
        services.AddScoped<IValidator<CreatePropertyRequest>, CreatePropertyRequestValidator>();
        services.AddScoped<IValidator<UpdatePropertyRequest>, UpdatePropertyRequestValidator>();
        return services;
    }

    public static IEndpointRouteBuilder MapPropertiesModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/properties");

        group.MapGet("", SearchAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);

        group.MapPost("", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreatePropertyRequest>>()
            .RequireAuthorization();

        group.MapPut("/{id:guid}", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdatePropertyRequest>>()
            .RequireAuthorization();

        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization();

        group.MapPost("/{id:guid}/photos", AddPhotoAsync)
            .DisableAntiforgery()
            .RequireAuthorization();

        group.MapDelete("/{id:guid}/photos/{photoId:guid}", DeletePhotoAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> SearchAsync(
        Guid? ownerId,
        string? city,
        string? country,
        decimal? minPrice,
        decimal? maxPrice,
        int? guests,
        DateOnly? checkIn,
        DateOnly? checkOut,
        PropertyService service,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20)
    {
        var query = new PropertyQuery(ownerId, city, country, minPrice, maxPrice, guests, checkIn, checkOut, page, pageSize);
        var result = await service.SearchAsync(query, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(Guid id, PropertyService service, CancellationToken cancellationToken)
    {
        var property = await service.GetByIdAsync(id, cancellationToken);
        return property is null ? Results.NotFound() : Results.Ok(property);
    }

    private static async Task<IResult> CreateAsync(CreatePropertyRequest request, ClaimsPrincipal user, PropertyService service, CancellationToken cancellationToken)
    {
        var ownerId = user.GetUserId();
        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var id = await service.CreateAsync(ownerId.Value, request, cancellationToken);
        return Results.Created($"/properties/{id}", new { id });
    }

    private static async Task<IResult> UpdateAsync(Guid id, UpdatePropertyRequest request, ClaimsPrincipal user, PropertyService service, CancellationToken cancellationToken)
    {
        var ownerId = user.GetUserId();
        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.UpdateAsync(ownerId.Value, id, request, cancellationToken);
        return MapStatus(result.Status) ?? Results.NoContent();
    }

    private static async Task<IResult> DeleteAsync(Guid id, ClaimsPrincipal user, PropertyService service, CancellationToken cancellationToken)
    {
        var ownerId = user.GetUserId();
        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.SoftDeleteAsync(ownerId.Value, id, cancellationToken);
        return MapStatus(result.Status) ?? Results.NoContent();
    }

    private static async Task<IResult> AddPhotoAsync(Guid id, IFormFile file, ClaimsPrincipal user, PropertyService service, FileStorage storage, CancellationToken cancellationToken)
    {
        var ownerId = user.GetUserId();
        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var validationError = FileValidation.ValidateImage(file);
        if (validationError is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: validationError);
        }

        var slot = await service.CheckPhotoSlotAsync(ownerId.Value, id, cancellationToken);
        var blocked = MapStatus(slot.Status);
        if (blocked is not null)
        {
            return blocked;
        }

        var objectName = $"{id}/{Guid.NewGuid():N}{Extension(file.ContentType)}";
        await using var stream = file.OpenReadStream();
        var url = await storage.UploadAsync(storage.PropertyPhotosBucket, objectName, stream, file.Length, file.ContentType, cancellationToken);

        var photo = await service.AddPhotoAsync(id, url, slot.PhotoCount, cancellationToken);
        return Results.Created($"/properties/{id}/photos/{photo.Id}", photo);
    }

    private static async Task<IResult> DeletePhotoAsync(Guid id, Guid photoId, ClaimsPrincipal user, PropertyService service, FileStorage storage, CancellationToken cancellationToken)
    {
        var ownerId = user.GetUserId();
        if (ownerId is null)
        {
            return Results.Unauthorized();
        }

        var result = await service.DeletePhotoAsync(ownerId.Value, id, photoId, cancellationToken);
        var blocked = MapStatus(result.Status);
        if (blocked is not null)
        {
            return blocked;
        }

        if (result.PhotoUrl is not null)
        {
            await storage.DeleteByUrlAsync(storage.PropertyPhotosBucket, result.PhotoUrl, cancellationToken);
        }

        return Results.NoContent();
    }

    private static IResult? MapStatus(PropertyMutationStatus status) => status switch
    {
        PropertyMutationStatus.Ok => null,
        PropertyMutationStatus.NotFound => Results.NotFound(),
        PropertyMutationStatus.Forbidden => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "You do not own this property"),
        PropertyMutationStatus.PhotoLimitReached => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Photo limit reached (max 10)"),
        _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request")
    };

    private static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };
}
