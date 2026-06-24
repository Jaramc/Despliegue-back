namespace RentalAI.Api.Modules.Auth;

public sealed record RegisterRequest(string Email, string Password, UserRole Role);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);

public sealed record MeResponse(Guid Id, string Email, string Role, DateTime CreatedAt);

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);

public enum AuthError
{
    None,
    EmailAlreadyExists,
    InvalidCredentials,
    InvalidRefreshToken
}

public sealed record AuthOutcome(AuthError Error, TokenPair? Tokens)
{
    public static AuthOutcome Success(TokenPair tokens) => new(AuthError.None, tokens);

    public static AuthOutcome Failed(AuthError error) => new(error, null);
}
