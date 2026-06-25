using Microsoft.AspNetCore.Http;

namespace RentalAI.Api.Modules.Files;

public static class FileValidation
{
    public const long MaxBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public static string? ValidateImage(IFormFile file)
    {
        if (file.Length == 0)
        {
            return "File is empty.";
        }

        if (file.Length > MaxBytes)
        {
            return "File exceeds the 5 MB limit.";
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return "Only JPG, PNG and WebP images are allowed.";
        }

        return null;
    }
}
