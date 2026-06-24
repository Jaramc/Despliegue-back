using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace RentalAI.Api.Modules.Kyc;

public sealed record ExtractedIdentity(string? FirstName, string? LastName, string? DocumentNumber, string? DateOfBirth);

public sealed class OpenAiVisionClient(HttpClient http, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ExtractedIdentity?> ExtractAsync(byte[] image, string contentType, CancellationToken cancellationToken)
    {
        var apiKey = configuration["OPENAI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        var model = configuration["OPENAI_MODEL_VISION"] ?? "gpt-4o";
        var maxTokens = int.Parse(configuration["OPENAI_MAX_TOKENS"] ?? "1024");
        var dataUrl = $"data:{contentType};base64,{Convert.ToBase64String(image)}";

        var payload = new
        {
            model,
            max_tokens = maxTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Extract the identity fields from this ID document. Return strict JSON with keys: firstName, lastName, documentNumber, dateOfBirth (YYYY-MM-DD). Use null when a field is unreadable." },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(payload);

        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var content = body.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content is null ? null : JsonSerializer.Deserialize<ExtractedIdentity>(content, JsonOptions);
    }
}
