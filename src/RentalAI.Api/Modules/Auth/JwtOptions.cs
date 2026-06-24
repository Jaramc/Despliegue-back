using Microsoft.Extensions.Configuration;

namespace RentalAI.Api.Modules.Auth;

public sealed class JwtOptions
{
    public required string Secret { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required int AccessTokenMinutes { get; init; }
    public required int RefreshTokenDays { get; init; }

    public static JwtOptions FromConfiguration(IConfiguration configuration) => new()
    {
        Secret = Required(configuration, "JWT_SECRET"),
        Issuer = Required(configuration, "JWT_ISSUER"),
        Audience = Required(configuration, "JWT_AUDIENCE"),
        AccessTokenMinutes = int.Parse(Required(configuration, "JWT_ACCESS_TOKEN_MINUTES")),
        RefreshTokenDays = int.Parse(Required(configuration, "JWT_REFRESH_TOKEN_DAYS"))
    };

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is not configured");
}
