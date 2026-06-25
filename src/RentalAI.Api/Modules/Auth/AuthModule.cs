using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using RentalAI.Api.Modules.Users;
using RentalAI.Common.Web;

namespace RentalAI.Api.Modules.Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, JwtOptions jwtOptions)
    {
        services.AddScoped<AuthService>();
        services.AddSingleton<TokenService>();
        services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<RefreshRequest>, RefreshRequestValidator>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        return services;
    }

    public static IEndpointRouteBuilder MapAuthModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/register", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/login", LoginAsync)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/refresh", RefreshAsync)
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapPost("/logout", LogoutAsync)
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy);

        group.MapGet("/me", GetMeAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(RegisterRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        var outcome = await authService.RegisterAsync(request, cancellationToken);
        return outcome.Error switch
        {
            AuthError.None => Results.Ok(ToResponse(outcome.Tokens!)),
            AuthError.EmailAlreadyExists => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Email already registered"),
            _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request")
        };
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        AuthService authService,
        WishlistService wishlistService,
        WishlistCookie wishlistCookie,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.LoginAsync(request, cancellationToken);
        if (outcome.Error != AuthError.None)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");
        }

        var anonymousWishlist = wishlistCookie.Read(httpContext.Request);
        if (anonymousWishlist.Count > 0 && outcome.UserId is { } userId)
        {
            await wishlistService.MergeAsync(userId, anonymousWishlist, cancellationToken);
            wishlistCookie.Clear(httpContext.Response);
        }

        return Results.Ok(ToResponse(outcome.Tokens!));
    }

    private static async Task<IResult> RefreshAsync(RefreshRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        var outcome = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return outcome.Error == AuthError.None
            ? Results.Ok(ToResponse(outcome.Tokens!))
            : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid refresh token");
    }

    private static async Task<IResult> LogoutAsync(RefreshRequest request, AuthService authService, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(ClaimsPrincipal principal, AuthService authService, CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        var me = await authService.GetMeAsync(userId, cancellationToken);
        return me is null ? Results.Unauthorized() : Results.Ok(me);
    }

    private static AuthResponse ToResponse(TokenPair tokens) =>
        new(tokens.AccessToken, tokens.RefreshToken, tokens.AccessTokenExpiresAt);
}
