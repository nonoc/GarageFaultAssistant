using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using GarageFaultAssistant.Models;
using Microsoft.Extensions.Logging;

namespace GarageFaultAssistant.Services;

public class OpenAiFaultAnalyzer : IFaultAnalyzer
{
    private const string Endpoint = "https://api.openai.com/v1";

    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OpenAiFaultAnalyzer>? _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string SystemPrompt = """
        You are an assistant for a garage service adviser. Your ONLY job is to extract
        and structure information from the customer's free-text description of a vehicle fault.

        RULES:
        - Do NOT diagnose the fault.
        - Do NOT invent facts. If the customer did not mention something, mark it as missing
          or empty; never fabricate vehicle details, symptoms or causes.
        - PossibleSystems must contain ONLY broad systems or areas to inspect, never a
          definitive cause.
        - Always return the fixed Disclaimer text exactly as specified in the schema.
        - The output must strictly match the provided JSON schema.
        """;

    private const string UserPromptTemplate = """
        Customer description:
        ---
        {0}
        ---

        Extract structured fault information from the text above following the schema.
        """;

    public OpenAiFaultAnalyzer(string apiKey, string model, ILogger<OpenAiFaultAnalyzer>? logger = null, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _model = model;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<FaultAnalysis> AnalyzeAsync(string description, CancellationToken cancellationToken = default)
    {
        var schemaJson = FaultAnalysisSchema.ToJsonSchema();

        var payload = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = string.Format(UserPromptTemplate, description) }
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "FaultAnalysis",
                    strict = true,
                    schema = JsonNode.Parse(schemaJson)
                }
            },
            temperature = 0.0
        };

        var url = $"{Endpoint}/chat/completions";
        using var response = await _httpClient.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("OpenAI request failed with status {StatusCode}. Response body: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var json = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(json))
        {
            _logger?.LogError("OpenAI response contained no message content.");
            throw new InvalidOperationException("Empty content in OpenAI response.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<FaultAnalysis>(json, JsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize AI response into FaultAnalysis.");

            if (parsed.Disclaimer is null || !parsed.Disclaimer.Contains("not a diagnosis"))
            {
                parsed = parsed with { Disclaimer = DefaultDisclaimer };
            }

            return parsed;
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse OpenAI response content as FaultAnalysis. Content: {Content}", json);
            return new FaultAnalysis(
                Summary: description,
                Symptoms: Array.Empty<string>(),
                VehicleContext: null,
                PossibleSystems: Array.Empty<string>(),
                Severity: FaultSeverity.Unknown,
                Urgency: FaultUrgency.Unknown,
                SuggestedQuestions: Array.Empty<string>(),
                NotProvided: new[] { "Unable to extract structured information (parse error)." },
                Disclaimer: DefaultDisclaimer
            );
        }
    }

    private static string DefaultDisclaimer => "This is not a diagnosis. The information above is derived from the customer's description only and is intended to help the adviser prepare questions and plan inspection steps. A physical inspection is required to identify the fault.";
}
