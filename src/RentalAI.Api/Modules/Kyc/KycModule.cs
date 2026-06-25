using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using RentalAI.Api.Modules.Files;
using RentalAI.Common.Web;

namespace RentalAI.Api.Modules.Kyc;

public static class KycModule
{
    public static IServiceCollection AddKycModule(this IServiceCollection services)
    {
        services.AddScoped<KycService>();
        services.AddScoped<KycCleanupJob>();
        services.AddScoped<AzureDocumentClient>();
        return services;
    }

    public static IEndpointRouteBuilder MapKycModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/kyc").RequireAuthorization();

        group.MapPost("/verify", VerifyAsync)
            .DisableAntiforgery()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapGet("/status", StatusAsync);

        return endpoints;
    }

    private static async Task<IResult> VerifyAsync(IFormFile file, ClaimsPrincipal user, KycService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var validationError = FileValidation.ValidateImage(file);
        if (validationError is not null)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: validationError);
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, cancellationToken);
        var result = await service.VerifyAsync(userId.Value, memory.ToArray(), cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> StatusAsync(ClaimsPrincipal user, KycService service, CancellationToken cancellationToken)
    {
        var userId = user.GetUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var status = await service.GetStatusAsync(userId.Value, cancellationToken);
        return Results.Ok(status);
    }
}
