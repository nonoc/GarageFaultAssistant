using System.Text.Json;

namespace GarageFaultAssistant.Services;

public class FakeFaultAnalyzer : IFaultAnalyzer
{
    private static readonly string[] BrakeSymptoms = { "brake", "brakes", "pedal", "squeal", "grinding", "ABS" };
    private static readonly string[] EngineSymptoms = { "rough idle", "misfire", "stalling", "knocking", "overheating", "coolant" };
    private static readonly string[] ElectricalSymptoms = { "battery", "alternator", "warning light", "dashboard", "lights", "fuse", "wiring" };
    private static readonly string[] TransmissionSymptoms = { "gear", "shifting", "clutch", "transmission", "automatic", "manual", "slip" };
    private static readonly string[] SuspensionSymptoms = { "suspension", "shock", "strut", "bump", "steering", "pull", "alignment" };
    private static readonly string[] SafetyCriticalSymptoms =
    {
        "smoke", "fire", "flames", "spark",
        "no brake", "can't stop", "cannot stop", "won't stop",
        "brake failure", "brakes failed", "lost brake", "loss of braking",
        "soft brake pedal", "soft pedal", "pedal feels soft", "spongy", "pedal to the floor"
    };

    public Task<Models.FaultAnalysis> AnalyzeAsync(string description, CancellationToken cancellationToken = default)
    {
        var lowered = description.ToLowerInvariant();
        var symptoms = new List<string>();
        var possibleSystems = new List<string>();
        var suggestedQuestions = new List<string>();
        var notProvided = new List<string>();

        if (BrakeSymptoms.Any(k => lowered.Contains(k)))
        {
            symptoms.Add("Brake noise / pedal concern");
            possibleSystems.Add("Braking system");
            suggestedQuestions.Add("Does the concern happen when braking, coasting, or both?");
            suggestedQuestions.Add("Any ABS warning light illuminated?");
        }
        if (EngineSymptoms.Any(k => lowered.Contains(k)))
        {
            symptoms.Add("Engine performance concern");
            possibleSystems.Add("Engine / cooling system");
            suggestedQuestions.Add("Any check-engine warning lights?");
        }
        if (ElectricalSymptoms.Any(k => lowered.Contains(k)))
        {
            symptoms.Add("Electrical or dashboard warning concern");
            possibleSystems.Add("Electrical / charging system");
            suggestedQuestions.Add("Which dashboard lights are illuminated?");
        }
        if (TransmissionSymptoms.Any(k => lowered.Contains(k)))
        {
            symptoms.Add("Transmission / driveline concern");
            possibleSystems.Add("Transmission / clutch system");
            suggestedQuestions.Add("Manual or automatic transmission?");
        }
        if (SuspensionSymptoms.Any(k => lowered.Contains(k)))
        {
            symptoms.Add("Steering / suspension concern");
            possibleSystems.Add("Suspension / steering");
            suggestedQuestions.Add("Does the pull occur under braking or at speed?");
        }

        if (!symptoms.Any())
        {
            symptoms.Add("General vehicle fault described by customer");
            possibleSystems.Add("Multiple possible systems");
            suggestedQuestions.Add("When does the issue occur (cold/hot, speed, load)?");
        }

        var isSafetyCritical = SafetyCriticalSymptoms.Any(k => lowered.Contains(k));

        if (isSafetyCritical)
        {
            symptoms.Add("Potential safety-critical symptom (smoke / fire / loss of braking)");
            possibleSystems.Insert(0, "Braking system — safety critical");
        }

        var urgency = isSafetyCritical
            ? Models.FaultUrgency.DoNotDrive
            : lowered.Contains("leak") || lowered.Contains("overheating")
                ? Models.FaultUrgency.BookSoon
                : Models.FaultUrgency.Routine;

        var severity = urgency == Models.FaultUrgency.DoNotDrive
            ? Models.FaultSeverity.High
            : urgency == Models.FaultUrgency.BookSoon
                ? Models.FaultSeverity.Medium
                : Models.FaultSeverity.Low;

        if (!lowered.Contains("year") && !lowered.Contains("model") && !lowered.Contains("make"))
        {
            notProvided.Add("Vehicle year, make and model");
        }
        if (!lowered.Contains("mileage") && !lowered.Contains("km"))
        {
            notProvided.Add("Current mileage");
        }
        if (!lowered.Contains("recent"))
        {
            notProvided.Add("Any recent repairs or servicing");
        }

        var result = new Models.FaultAnalysis(
            Summary: "Customer describes a possible concern related to " + string.Join(" and ", possibleSystems.Take(2)) + ".",
            Symptoms: symptoms.Distinct().ToArray(),
            VehicleContext: null,
            PossibleSystems: possibleSystems.Distinct().ToArray(),
            Severity: severity,
            Urgency: urgency,
            SuggestedQuestions: suggestedQuestions.Distinct().ToArray(),
            NotProvided: notProvided.Distinct().ToArray(),
            Disclaimer: "This is not a diagnosis. The information above is derived from the customer's description only and is intended to help the adviser prepare questions and plan inspection steps. A physical inspection is required to identify the fault."
        );

        return Task.FromResult(result);
    }
}
