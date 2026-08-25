using System.Text.Json;
using System.Text.Json.Nodes;

namespace GarageFaultAssistant.Models;

public record FaultAnalysis(
    string Summary,
    string[] Symptoms,
    string? VehicleContext,
    string[] PossibleSystems,
    FaultSeverity Severity,
    FaultUrgency Urgency,
    string[] SuggestedQuestions,
    string[] NotProvided,
    string Disclaimer
);

public enum FaultSeverity
{
    Unknown,
    Low,
    Medium,
    High
}

public enum FaultUrgency
{
    Unknown,
    Routine,
    BookSoon,
    DoNotDrive
}

public static class FaultAnalysisSchema
{
    public static string ToJsonSchema()
    {
        var schema = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["Summary"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "One-sentence paraphrase of the reported fault."
                },
                ["Symptoms"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Symptoms stated by the customer."
                },
                ["VehicleContext"] = new JsonObject
                {
                    ["type"] = new JsonArray("string", "null"),
                    ["description"] = "Make/model/year/engine if mentioned; otherwise null."
                },
                ["PossibleSystems"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Systems to investigate. Not a diagnosis."
                },
                ["Severity"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray("Unknown", "Low", "Medium", "High")
                },
                ["Urgency"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray("Unknown", "Routine", "BookSoon", "DoNotDrive")
                },
                ["SuggestedQuestions"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Info the adviser should gather."
                },
                ["NotProvided"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Key facts the customer did not state."
                },
                ["Disclaimer"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Fixed text stating this is not a diagnosis."
                }
            },
            ["required"] = new JsonArray("Summary", "Symptoms", "VehicleContext", "PossibleSystems", "Severity", "Urgency", "SuggestedQuestions", "NotProvided", "Disclaimer"),
            ["additionalProperties"] = false
        };

        return schema.ToJsonString();
    }
}
