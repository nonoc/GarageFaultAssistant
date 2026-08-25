using GarageFaultAssistant.Models;
using GarageFaultAssistant.Services;

namespace GarageFaultAssistant.Tests;

public class FakeFaultAnalyzerTests
{
    private readonly FakeFaultAnalyzer _analyzer = new();

    [Fact]
    public async Task AnalyzeAsync_IncludesBrakeSymptoms_WhenBrakeKeywordPresent()
    {
        var result = await _analyzer.AnalyzeAsync("Squealing noise when I press the brake pedal.");
        Assert.Contains(result.Symptoms, s => s.Contains("Brake noise", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.PossibleSystems, p => p.Contains("Braking system", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_MarksDoNotDrive_ForSafetyCriticalSymptoms()
    {
        var result = await _analyzer.AnalyzeAsync("I see smoke and sparks and I can't stop the car.");
        Assert.Equal(FaultUrgency.DoNotDrive, result.Urgency);
        Assert.Equal(FaultSeverity.High, result.Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_MarksDoNotDrive_ForSoftBrakePedal()
    {
        var result = await _analyzer.AnalyzeAsync("Grinding noise when braking and soft brake pedal.");
        Assert.Equal(FaultUrgency.DoNotDrive, result.Urgency);
        Assert.Equal(FaultSeverity.High, result.Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_MarksDoNotDrive_ForLossOfBraking()
    {
        var result = await _analyzer.AnalyzeAsync("The brakes failed and the pedal went to the floor.");
        Assert.Equal(FaultUrgency.DoNotDrive, result.Urgency);
        Assert.Equal(FaultSeverity.High, result.Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_AlwaysPopulatesDisclaimer()
    {
        var result = await _analyzer.AnalyzeAsync("Strange rattle over bumps.");
        Assert.False(string.IsNullOrWhiteSpace(result.Disclaimer));
        Assert.Contains("not a diagnosis", result.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_RecordsMissingVehicleInfo()
    {
        var result = await _analyzer.AnalyzeAsync("The car is pulling to the left.");
        Assert.Contains(result.NotProvided, n => n.Contains("Vehicle year", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.NotProvided, n => n.Contains("mileage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsync_IsDeterministicForSameInput()
    {
        var a = await _analyzer.AnalyzeAsync("Warning light came on and the battery is flat.");
        var b = await _analyzer.AnalyzeAsync("Warning light came on and the battery is flat.");
        Assert.Equal(a.Summary, b.Summary);
        Assert.Equal(a.Urgency, b.Urgency);
    }
}
