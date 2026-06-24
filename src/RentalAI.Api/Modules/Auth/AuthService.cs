using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;

namespace RentalAI.Api.Modules.Auth;

public sealed class AuthService(AppDbContext db, IPasswordHasher<User> passwordHasher, TokenService tokenService)
{
    public async Task<AuthOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return AuthOutcome.Failed(AuthError.EmailAlreadyExists);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Role = request.Role,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);

        var tokens = IssueTokens(user);
        await db.SaveChangesAsync(cancellationToken);
        return AuthOutcome.Success(tokens);
    }

    public async Task<AuthOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = Normalize(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return AuthOutcome.Failed(AuthError.InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return AuthOutcome.Failed(AuthError.InvalidCredentials);
        }

        var tokens = IssueTokens(user);
        await db.SaveChangesAsync(cancellationToken);
        return AuthOutcome.Success(tokens, user.Id);
    }

    public async Task<AuthOutcome> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
        {
            return AuthOutcome.Failed(AuthError.InvalidRefreshToken);
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken);
        if (user is null)
        {
            return AuthOutcome.Failed(AuthError.InvalidRefreshToken);
        }

        stored.RevokedAt = DateTime.UtcNow;
        var tokens = IssueTokens(user);
        await db.SaveChangesAsync(cancellationToken);
        return AuthOutcome.Success(tokens);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = TokenService.Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, cancellationToken);
        if (stored is null)
        {
            return;
        }

        stored.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MeResponse?> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null
            ? null
            : new MeResponse(user.Id, user.Email, user.Role.ToString(), user.CreatedAt);
    }

    private TokenPair IssueTokens(User user)
    {
        var access = tokenService.CreateAccessToken(user);
        var refresh = tokenService.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refresh.Hash,
            ExpiresAt = refresh.ExpiresAt,
            CreatedAt = DateTime.UtcNow
        });

        return new TokenPair(access.Value, refresh.Value, access.ExpiresAt);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
