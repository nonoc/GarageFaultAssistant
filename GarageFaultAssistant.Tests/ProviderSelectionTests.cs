using GarageFaultAssistant.Services;
using Microsoft.Extensions.Configuration;

namespace GarageFaultAssistant.Tests;

public class ProviderSelectionTests
{
    [Fact]
    public void FakeProvider_ResolvesFakeFaultAnalyzer()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Provider"] = "Fake"
            })
            .Build();

        IFaultAnalyzer analyzer = config.GetValue<string>("Ai:Provider") switch
        {
            "OpenAI" => new OpenAiFaultAnalyzer("test-key", "gpt-4.1-mini"),
            _ => new FakeFaultAnalyzer()
        };

        Assert.IsType<FakeFaultAnalyzer>(analyzer);
    }

    [Fact]
    public void OpenAIProvider_ResolvesOpenAiFaultAnalyzer_WhenConfigured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Provider"] = "OpenAI",
                ["Ai:OpenAI:ApiKey"] = "test-key",
                ["Ai:OpenAI:Model"] = "gpt-4.1-mini"
            })
            .Build();

        IFaultAnalyzer analyzer = config.GetValue<string>("Ai:Provider") switch
        {
            "OpenAI" => new OpenAiFaultAnalyzer(
                config["Ai:OpenAI:ApiKey"]!,
                config["Ai:OpenAI:Model"]!),
            _ => new FakeFaultAnalyzer()
        };

        Assert.IsType<OpenAiFaultAnalyzer>(analyzer);
    }
}
