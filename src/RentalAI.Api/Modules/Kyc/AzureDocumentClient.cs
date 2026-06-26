using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RentalAI.Api.Modules.Kyc;

public sealed record ExtractedIdentity(string? FirstName, string? LastName, string? DocumentNumber, string? DateOfBirth);

public sealed class AzureDocumentClient(IConfiguration configuration, ILogger<AzureDocumentClient> logger)
{
    private static readonly Regex NumberPattern = new(@"\d[\d.]*\d", RegexOptions.Compiled);

    private static readonly string[] LabelTokens =
    {
        "NUMERO", "NUIP", "APELLIDOS", "NOMBRES", "FECHA", "NACIMIENTO", "SEXO", "LUGAR", "ESTATURA"
    };

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
        var operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", stream, cancellationToken: timeout.Token);

        var lines = operation.Value.Pages
            .SelectMany(page => page.Lines)
            .Select(line => line.Content.Trim())
            .Where(content => content.Length > 0)
            .ToList();

        logger.LogInformation("KYC_OCR_RAW: {Text}", string.Join(" | ", lines));

        if (lines.Count == 0)
        {
            return null;
        }

        var identity = new ExtractedIdentity(
            FindValue(lines, "NOMBRES"),
            FindValue(lines, "APELLIDOS"),
            FindNumber(lines, "NUMERO", "NUIP"),
            FindValue(lines, "FECHA DE NACIMIENTO"));

        logger.LogInformation(
            "KYC_OCR_PARSED: FirstName={FirstName} LastName={LastName} DocumentNumber={DocumentNumber} DateOfBirth={DateOfBirth}",
            identity.FirstName,
            identity.LastName,
            identity.DocumentNumber,
            identity.DateOfBirth);

        return identity;
    }

    private static string? FindValue(IReadOnlyList<string> lines, string label)
    {
        var target = Normalize(label);
        for (var i = 0; i < lines.Count; i++)
        {
            var normalized = Normalize(lines[i]);
            var index = normalized.IndexOf(target, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            var inline = lines[i][(index + target.Length)..].Trim(' ', ':', '-', '.', '\t');
            if (inline.Length > 0)
            {
                return inline;
            }

            for (var j = i + 1; j < lines.Count; j++)
            {
                if (!IsLabel(lines[j]))
                {
                    return lines[j];
                }
            }

            return null;
        }

        return null;
    }

    private static string? FindNumber(IReadOnlyList<string> lines, params string[] labels)
    {
        foreach (var label in labels)
        {
            var value = FindValue(lines, label);
            if (value is not null && TryNumber(value, out var labelled))
            {
                return labelled;
            }
        }

        foreach (var line in lines)
        {
            if (TryNumber(line, out var fallback))
            {
                return fallback;
            }
        }

        return null;
    }

    private static bool TryNumber(string text, out string? number)
    {
        foreach (Match match in NumberPattern.Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length is >= 6 and <= 12)
            {
                number = digits;
                return true;
            }
        }

        number = null;
        return false;
    }

    private static bool IsLabel(string line)
    {
        var normalized = Normalize(line);
        return LabelTokens.Any(token => normalized.Contains(token, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var baseCharacter = character.ToString().Normalize(NormalizationForm.FormD)[0];
            if (CharUnicodeInfo.GetUnicodeCategory(baseCharacter) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToUpperInvariant(baseCharacter));
            }
        }

        return builder.ToString();
    }
}
