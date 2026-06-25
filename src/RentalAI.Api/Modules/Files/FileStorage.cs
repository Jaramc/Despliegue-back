using Minio;
using Minio.DataModel.Args;

namespace RentalAI.Api.Modules.Files;

public sealed class FileStorage(IMinioClient client, FileStorageOptions options)
{
    public string PropertyPhotosBucket => options.PropertyPhotosBucket;

    public string KycBucket => options.KycBucket;

    public async Task EnsureBucketsAsync(CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(options.PropertyPhotosBucket, publicRead: true, cancellationToken);
        await EnsureBucketAsync(options.KycBucket, publicRead: false, cancellationToken);
    }

    public async Task<string> UploadAsync(string bucket, string objectName, Stream content, long size, string contentType, CancellationToken cancellationToken)
    {
        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType), cancellationToken);

        return $"{options.PublicUrl}/{bucket}/{objectName}";
    }

    public Task DeleteAsync(string bucket, string objectName, CancellationToken cancellationToken) =>
        client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(objectName), cancellationToken);

    public Task DeleteByUrlAsync(string bucket, string url, CancellationToken cancellationToken)
    {
        var prefix = $"{options.PublicUrl}/{bucket}/";
        var objectName = url.StartsWith(prefix) ? url[prefix.Length..] : url;
        return DeleteAsync(bucket, objectName, cancellationToken);
    }

    private async Task EnsureBucketAsync(string bucket, bool publicRead, CancellationToken cancellationToken)
    {
        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), cancellationToken);
        if (!exists)
        {
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
        }

        if (publicRead)
        {
            await client.SetPolicyAsync(new SetPolicyArgs().WithBucket(bucket).WithPolicy(PublicReadPolicy(bucket)), cancellationToken);
        }
    }

    private static string PublicReadPolicy(string bucket) =>
        $$"""
        {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"AWS":["*"]},"Action":["s3:GetObject"],"Resource":["arn:aws:s3:::{{bucket}}/*"]}]}
        """;
}
