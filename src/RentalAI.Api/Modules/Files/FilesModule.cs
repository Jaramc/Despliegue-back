using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace RentalAI.Api.Modules.Files;

public static class FilesModule
{
    public static IServiceCollection AddFilesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var options = FileStorageOptions.FromConfiguration(configuration);
        services.AddSingleton(options);
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .Build());
        services.AddSingleton<FileStorage>();
        return services;
    }
}
