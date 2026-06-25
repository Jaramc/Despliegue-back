using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RentalAI.Api.Data;
using RentalAI.Api.Modules.Auth;
using RentalAI.Api.Modules.Files;
using RentalAI.Api.Modules.Notifications;

namespace RentalAI.Api.Modules.Kyc;

public sealed class KycService(
    AppDbContext db,
    FileStorage storage,
    AzureDocumentClient documents,
    IBackgroundJobClient jobs,
    IConfiguration configuration,
    NotificationDispatcher notifications,
    ILogger<KycService> logger)
{
    public async Task<KycVerifyResponse> VerifyAsync(Guid userId, byte[] document, CancellationToken cancellationToken)
    {
        var encrypted = DocumentCrypto.Encrypt(document, Required("KYC_ENCRYPTION_KEY"), userId);
        var objectName = $"{userId}/{Guid.NewGuid():N}.enc";

        using (var encryptedStream = new MemoryStream(encrypted))
        {
            await storage.UploadAsync(storage.KycBucket, objectName, encryptedStream, encrypted.Length, "application/octet-stream", cancellationToken);
        }

        var ttlHours = int.Parse(configuration["KYC_DOCUMENT_TTL_HOURS"] ?? "24");
        jobs.Schedule<KycCleanupJob>(job => job.DeleteDocumentAsync(storage.KycBucket, objectName), TimeSpan.FromHours(ttlHours));

        var extracted = await SafeExtractAsync(document, cancellationToken);
        var (verdict, reason) = Evaluate(extracted);

        var extractedJson = JsonSerializer.Serialize(extracted ?? new ExtractedIdentity(null, null, null, null));
        var hashedData = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(extractedJson)));

        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        if (verdict == KycVerdict.Approved && user.Role == UserRole.Guest)
        {
            user.Role = UserRole.Owner;
            await RevokeActiveTokensAsync(userId, cancellationToken);
        }

        await UpsertAsync(userId, hashedData, verdict, cancellationToken);

        notifications.KycResult(userId, user.Email, verdict == KycVerdict.Approved, reason);

        return new KycVerifyResponse(verdict.ToString(), reason);
    }

    public async Task<KycStatusResponse> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var verification = await db.KycVerifications.AsNoTracking().SingleOrDefaultAsync(k => k.UserId == userId, cancellationToken);
        return verification is null
            ? new KycStatusResponse(KycVerdict.Pending.ToString(), null)
            : new KycStatusResponse(verification.Verdict.ToString(), verification.VerifiedAt);
    }

    private async Task<ExtractedIdentity?> SafeExtractAsync(byte[] document, CancellationToken cancellationToken)
    {
        try
        {
            return await documents.ExtractAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "KYC document extraction failed");
            return null;
        }
    }

    private static (KycVerdict Verdict, string? Reason) Evaluate(ExtractedIdentity? extracted)
    {
        if (extracted is null)
        {
            return (KycVerdict.Rejected, "Document could not be processed.");
        }

        var complete = !string.IsNullOrWhiteSpace(extracted.FirstName)
            && !string.IsNullOrWhiteSpace(extracted.LastName)
            && !string.IsNullOrWhiteSpace(extracted.DocumentNumber)
            && !string.IsNullOrWhiteSpace(extracted.DateOfBirth);

        return complete
            ? (KycVerdict.Approved, null)
            : (KycVerdict.Rejected, "Required identity fields are missing or unreadable.");
    }

    private async Task UpsertAsync(Guid userId, string hashedData, KycVerdict verdict, CancellationToken cancellationToken)
    {
        var verification = await db.KycVerifications.SingleOrDefaultAsync(k => k.UserId == userId, cancellationToken);
        var now = DateTime.UtcNow;

        if (verification is null)
        {
            db.KycVerifications.Add(new KycVerification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DocumentType = "national-id",
                ExtractedData = hashedData,
                Verdict = verdict,
                VerifiedAt = now,
                CreatedAt = now
            });
        }
        else
        {
            verification.ExtractedData = hashedData;
            verification.Verdict = verdict;
            verification.VerifiedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevokeActiveTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }
    }

    private string Required(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is not configured");
}
