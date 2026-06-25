using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;

namespace RentalAI.Api.Modules.Kyc;

public sealed record ExtractedIdentity(string? FirstName, string? LastName, string? DocumentNumber, string? DateOfBirth);

public sealed class AzureDocumentClient(IConfiguration configuration)
{
    public async Task<ExtractedIdentity?> ExtractAsync(byte[] image, CancellationToken cancellationToken)
    {
        var endpoint = configuration["AZURE_DOCUMENT_ENDPOINT"];
        var key = configuration["AZURE_DOCUMENT_KEY"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var client = new DocumentAnalysisClient(new Uri(endpoint), new AzureKeyCredential(key));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        using var stream = new MemoryStream(image);
        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-idDocument", stream, cancellationToken: timeout.Token);

        var document = operation.Value.Documents.FirstOrDefault();
        if (document is null)
        {
            return null;
        }

        return new ExtractedIdentity(
            ReadField(document, "FirstName"),
            ReadField(document, "LastName"),
            ReadField(document, "DocumentNumber"),
            ReadField(document, "DateOfBirth"));
    }

    private static string? ReadField(AnalyzedDocument document, string name) =>
        document.Fields.TryGetValue(name, out var field) ? field.Content : null;
}
