using RentalAI.Api.Modules.Files;

namespace RentalAI.Api.Modules.Kyc;

public sealed class KycCleanupJob(FileStorage storage)
{
    public Task DeleteDocumentAsync(string bucket, string objectName) =>
        storage.DeleteAsync(bucket, objectName, CancellationToken.None);
}
