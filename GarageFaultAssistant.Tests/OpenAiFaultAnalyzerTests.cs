using System.Net;
using System.Text;
using System.Text.Json;
using GarageFaultAssistant.Models;
using GarageFaultAssistant.Services;

namespace GarageFaultAssistant.Tests;

public class OpenAiFaultAnalyzerTests
{
    private static string BuildOpenAiResponseBody(string contentJson)
    {
        var response = new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            created = 1,
            model = "gpt-4.1-mini",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = contentJson },
                    finish_reason = "stop"
                }
            }
        };
        return JsonSerializer.Serialize(response);
    }

    private static HttpClient StubClient(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        });
        return new HttpClient(handler);
    }

    [Fact]
    public async Task AnalyzeAsync_DeserializesStringEnumValues()
    {
        var content = JsonSerializer.Serialize(new
        {
            Summary = "Brake concern.",
            Symptoms = new[] { "Squealing", "Soft pedal" },
            VehicleContext = (string?)null,
            PossibleSystems = new[] { "Braking system" },
            Severity = "High",
            Urgency = "DoNotDrive",
            SuggestedQuestions = Array.Empty<string>(),
            NotProvided = Array.Empty<string>(),
            Disclaimer = "This is not a diagnosis."
        });

        var analyzer = new OpenAiFaultAnalyzer("test-key", "gpt-4.1-mini", httpClient: StubClient(BuildOpenAiResponseBody(content)));

        var result = await analyzer.AnalyzeAsync("Grinding noise and soft brake pedal.");

        Assert.Equal(FaultSeverity.High, result.Severity);
        Assert.Equal(FaultUrgency.DoNotDrive, result.Urgency);
        Assert.Equal("Brake concern.", result.Summary);
        Assert.Equal("Braking system", result.PossibleSystems[0]);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFallback_WhenContentIsNotJson()
    {
        var analyzer = new OpenAiFaultAnalyzer("test-key", "gpt-4.1-mini", httpClient: StubClient(BuildOpenAiResponseBody("not-json")));

        var result = await analyzer.AnalyzeAsync("Some fault");

        Assert.Equal(FaultSeverity.Unknown, result.Severity);
        Assert.Equal(FaultUrgency.Unknown, result.Urgency);
        Assert.Contains(result.NotProvided, n => n.Contains("parse error"));
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFallback_WhenEnumValueIsInvalid()
    {
        var content = JsonSerializer.Serialize(new
        {
            Summary = "Brake concern.",
            Symptoms = new[] { "Squealing" },
            VehicleContext = (string?)null,
            PossibleSystems = new[] { "Braking system" },
            Severity = "Catastrophic",
            Urgency = "DoNotDrive",
            SuggestedQuestions = Array.Empty<string>(),
            NotProvided = Array.Empty<string>(),
            Disclaimer = "This is not a diagnosis."
        });

        var analyzer = new OpenAiFaultAnalyzer("test-key", "gpt-4.1-mini", httpClient: StubClient(BuildOpenAiResponseBody(content)));

        var result = await analyzer.AnalyzeAsync("Some fault");

        Assert.Equal(FaultSeverity.Unknown, result.Severity);
        Assert.Contains(result.NotProvided, n => n.Contains("parse error"));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
