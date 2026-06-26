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
    private static readonly Regex NumberPattern = new(@"\d[\d.\s]*\d", RegexOptions.Compiled);

    private static readonly Regex DatePattern = new(
        @"\d{1,2}[-/](?:[A-Za-zÁÉÍÓÚáéíóú]{3,}|\d{1,2})[-/]\d{2,4}",
        RegexOptions.Compiled);

    private static readonly string[] SurnameLabels = { "APELLIDOS" };
    private static readonly string[] GivenNameLabels = { "NOMBRES" };
    private static readonly string[] NumberLabels = { "NUMERO", "NUIP" };
    private static readonly string[] LabelTokens =
    {
        "APELLIDOS", "NOMBRES", "NUMERO", "NUIP", "FECHA", "NACIMIENTO", "SEXO", "LUGAR", "ESTATURA"
    };

    private static readonly HashSet<string> NoiseWords = new(StringComparer.Ordinal)
    {
        "REPUBLICA", "COLOMBIA", "IDENTIFICACION", "PERSONAL", "CEDULA", "CIUDADANIA",
        "FIRMA", "PUBLICA", "OLOMBI", "REP", "CO", "NUMERO", "NUIP", "APELLIDOS",
        "NOMBRES", "FECHA", "NACIMIENTO", "LUGAR", "SEXO", "ESTATURA"
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
            FindName(lines, GivenNameLabels, preferUpper: false),
            FindName(lines, SurnameLabels, preferUpper: true),
            FindNumber(lines),
            FindDate(lines));

        logger.LogInformation(
            "KYC_OCR_PARSED: FirstName={FirstName} LastName={LastName} DocumentNumber={DocumentNumber} DateOfBirth={DateOfBirth}",
            identity.FirstName,
            identity.LastName,
            identity.DocumentNumber,
            identity.DateOfBirth);

        return identity;
    }

    private static string? FindName(IReadOnlyList<string> lines, string[] labels, bool preferUpper)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!labels.Any(label => LabelMatches(lines[i], label)))
            {
                continue;
            }

            var best = Neighbors(lines, i)
                .Where(IsNameCandidate)
                .Select(candidate => (Value: candidate, Score: ScoreName(candidate, preferUpper)))
                .OrderByDescending(candidate => candidate.Score)
                .FirstOrDefault();

            if (best.Value is not null)
            {
                return best.Value;
            }
        }

        return null;
    }

    private static string? FindNumber(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!NumberLabels.Any(label => LabelMatches(lines[i], label)))
            {
                continue;
            }

            foreach (var neighbor in Neighbors(lines, i))
            {
                if (TryNumber(neighbor, 6, out var labelled))
                {
                    return labelled;
                }
            }
        }

        foreach (var line in lines)
        {
            if (TryNumber(line, 8, out var fallback))
            {
                return fallback;
            }
        }

        return null;
    }

    private static string? FindDate(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (!LabelMatches(lines[i], "NACIMIENTO"))
            {
                continue;
            }

            foreach (var neighbor in Neighbors(lines, i))
            {
                var match = DatePattern.Match(neighbor);
                if (match.Success)
                {
                    return match.Value;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> Neighbors(IReadOnlyList<string> lines, int index)
    {
        if (index + 1 < lines.Count)
        {
            yield return lines[index + 1];
        }

        yield return lines[index];

        if (index - 1 >= 0)
        {
            yield return lines[index - 1];
        }
    }

    private static bool IsNameCandidate(string line)
    {
        if (line.Trim().Length < 3 || line.Any(char.IsDigit))
        {
            return false;
        }

        if (LabelTokens.Any(label => LabelMatches(line, label)))
        {
            return false;
        }

        return MeaningfulTokens(line).Any();
    }

    private static int ScoreName(string line, bool preferUpper)
    {
        var tokens = MeaningfulTokens(line).Count();
        var alpha = line.Count(char.IsLetter);
        var hasLower = line.Any(char.IsLower);
        var caseBonus = preferUpper ? (hasLower ? 0 : 5) : (hasLower ? 5 : 0);
        return tokens * 100 + alpha + caseBonus;
    }

    private static IEnumerable<string> MeaningfulTokens(string line) =>
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.All(char.IsLetter)
                && Normalize(token).Length >= 3
                && !NoiseWords.Contains(Normalize(token)));

    private static bool TryNumber(string text, int minDigits, out string? number)
    {
        foreach (Match match in NumberPattern.Matches(text))
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            if (digits.Length >= minDigits && digits.Length <= 12)
            {
                number = digits;
                return true;
            }
        }

        number = null;
        return false;
    }

    private static bool LabelMatches(string line, string label)
    {
        var normalized = Normalize(line);
        var target = Normalize(label);
        if (normalized.Contains(target, StringComparison.Ordinal))
        {
            return true;
        }

        for (var drop = 1; drop <= 3; drop++)
        {
            if (target.Length - drop >= 5 && normalized.Contains(target[drop..], StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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
