using GarageFaultAssistant.Models;
using GarageFaultAssistant.Services;

namespace GarageFaultAssistant.Tests;

public record EvaluationExample(string Description, string[] ExpectedSymptoms, FaultSeverity ExpectedSeverity);

public static class EvaluationScorer
{
    public static double Score(FaultAnalysis actual, EvaluationExample expected)
    {
        var matchedExpected = expected.ExpectedSymptoms.Count(e => actual.Symptoms.Any(s => s.Contains(e, StringComparison.OrdinalIgnoreCase)));
        var symptoms = expected.ExpectedSymptoms.Length == 0 ? 1.0 : matchedExpected / (double)expected.ExpectedSymptoms.Length;
        var severity = actual.Severity == expected.ExpectedSeverity ? 1.0 : 0.0;
        var noDiagnosis = actual.PossibleSystems.All(p => !p.Contains("diagnose", StringComparison.OrdinalIgnoreCase)) ? 1.0 : 0.0;

        return (symptoms * 0.4) + (severity * 0.3) + (noDiagnosis * 0.3);
    }
}

public class EvaluationTests
{
    private readonly FakeFaultAnalyzer _analyzer = new();

    public static IEnumerable<object[]> Examples => new[]
    {
        new object[] { new EvaluationExample("Squealing noise when braking and the pedal feels soft.", new[] { "Brake noise / pedal concern" }, FaultSeverity.High) },
        new object[] { new EvaluationExample("Warning light on dashboard and the battery keeps dying.", new[] { "Electrical or dashboard warning concern" }, FaultSeverity.Low) },
        new object[] { new EvaluationExample("Car is overheating and steam is coming from the bonnet.", new[] { "Engine performance concern" }, FaultSeverity.Medium) },
        new object[] { new EvaluationExample("Rough idle and occasional misfire.", new[] { "Engine performance concern" }, FaultSeverity.Low) },
        new object[] { new EvaluationExample("Car pulls to the left and there is a clunking noise over bumps.", new[] { "Steering / suspension concern" }, FaultSeverity.Low) }
    };

    [Theory]
    [MemberData(nameof(Examples))]
    public async Task FakeAnalyzer_ScoresReasonablyOnEvaluationExamples(EvaluationExample example)
    {
        var result = await _analyzer.AnalyzeAsync(example.Description);
        var score = EvaluationScorer.Score(result, example);
        Assert.True(score >= 0.6, $"Score {score:F2} was below threshold for: {example.Description}");
    }

    [Fact]
    public async Task Scorer_Returns1_WhenAllFieldsMatch()
    {
        var actual = new FaultAnalysis(
            Summary: "Brake concern.",
            Symptoms: new[] { "Brake noise / pedal concern" },
            VehicleContext: null,
            PossibleSystems: new[] { "Braking system" },
            Severity: FaultSeverity.High,
            Urgency: FaultUrgency.BookSoon,
            SuggestedQuestions: Array.Empty<string>(),
            NotProvided: Array.Empty<string>(),
            Disclaimer: "not a diagnosis"
        );
        var expected = new EvaluationExample("Brake concern.", new[] { "Brake noise / pedal concern" }, FaultSeverity.High);
        var score = EvaluationScorer.Score(actual, expected);
        Assert.Equal(1.0, score, precision: 2);
    }
}
