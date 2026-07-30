using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProductStoreAPI.Contracts;

namespace ProductStoreAPI.Services;

public class OllamaProductScanner : IProductScanner
{
    private const string Prompt =
        "You are cataloguing a second-hand item for a product listing. " +
        "Look at the photos and suggest a short product name, a one or two sentence " +
        "description of the item and its condition, and a broad category such as " +
        "Furniture, Electronics, Clothing, or Books. Describe only what you can see. " +
        "If you cannot tell, leave the field empty rather than guessing.";

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly string _model;
    private readonly int _maxEdge;
    private readonly int _contextTokens;

    public OllamaProductScanner(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _model = configuration["Ollama:Model"] ?? "qwen2.5vl:3b";
        _maxEdge = configuration.GetValue("Ollama:MaxImageEdge", 768);
        _contextTokens = configuration.GetValue("Ollama:ContextTokens", 8192);
    }

    public async Task<ScanSuggestion> ScanAsync(IReadOnlyList<ScanImage> images, CancellationToken ct)
    {
        var encoded = images
            .Select(i => Convert.ToBase64String(ScanImageResizer.ToJpeg(i.Content, _maxEdge, 85)))
            .ToArray();

        var request = new
        {
            model = _model,
            stream = false,
            options = new { num_ctx = _contextTokens },
            format = new
            {
                type = "object",
                properties = new
                {
                    name = new { type = "string" },
                    description = new { type = "string" },
                    category = new { type = "string" },
                },
                required = new[] { "name", "description", "category" },
            },
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = Prompt,
                    images = encoded,
                },
            },
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Ollama returned {(int)response.StatusCode} for model '{_model}': {error}");
        }

        var chat = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(JsonOptions, ct);
        var content = chat?.Message?.Content;
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Ollama returned no content for model '{_model}'.");
        }

        return JsonSerializer.Deserialize<ScanSuggestion>(content, JsonOptions)
            ?? throw new InvalidOperationException($"Ollama returned unreadable JSON: {content}");
    }

    private sealed record OllamaChatResponse([property: JsonPropertyName("message")] OllamaMessage? Message);

    private sealed record OllamaMessage([property: JsonPropertyName("content")] string? Content);
}
