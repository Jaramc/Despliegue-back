using Microsoft.Extensions.Configuration;

namespace RentalAI.Api.Modules.Files;

public sealed class FileStorageOptions
{
    public required string Endpoint { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string PublicUrl { get; init; }
    public required string PropertyPhotosBucket { get; init; }
    public required string KycBucket { get; init; }

    public static FileStorageOptions FromConfiguration(IConfiguration configuration) => new()
    {
        Endpoint = $"{configuration["MINIO_HOST"] ?? "localhost"}:{configuration["MINIO_PORT"] ?? "9000"}",
        AccessKey = Required(configuration, "MINIO_ROOT_USER"),
        SecretKey = Required(configuration, "MINIO_ROOT_PASSWORD"),
        PublicUrl = (configuration["MINIO_PUBLIC_URL"] ?? "http://localhost:9000").TrimEnd('/'),
        PropertyPhotosBucket = configuration["MINIO_BUCKET_PROPERTIES"] ?? "property-photos",
        KycBucket = configuration["MINIO_BUCKET_KYC"] ?? "kyc-documents"
    };

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is not configured");
}
