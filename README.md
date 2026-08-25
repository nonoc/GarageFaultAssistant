# Garage Fault Assistant

Take-home exercise: an ASP.NET Core Razor Pages app that turns a customer's free-text vehicle fault description into structured information for a garage service adviser.

## What it does

- Accepts a free-text fault description.
- Returns structured output: summary, extracted symptoms, vehicle context (if mentioned), **possible systems to inspect** (explicitly **not** a diagnosis), severity, urgency, suggested questions for the adviser, missing information, and a fixed disclaimer.
- Includes a demo/fake AI provider so the app runs locally without any API key.
- Supports OpenAI (JSON schema structured outputs) when configured.

## Running

### Fake provider (no API key required)

```bash
cd GarageFaultAssistant
dotnet run
```

Navigate to the home page, enter a description, and click **Analyse**. The fake provider uses keyword-based rules and is deterministic.

### OpenAI provider

1. Set the API key and model via User Secrets or environment variables.

```bash
dotnet user-secrets init --project GarageFaultAssistant
dotnet user-secrets set "Ai:OpenAI:ApiKey" "<your-api-key>" --project GarageFaultAssistant
dotnet user-secrets set "Ai:OpenAI:Model" "gpt-4.1-mini" --project GarageFaultAssistant
```

The resulting `secrets.json` looks like:

```json
{
  "Ai:OpenAI:ApiKey": "<your-api-key>",
  "Ai:OpenAI:Model": "gpt-4.1-mini"
}
```

2. Switch the provider in `appsettings.json`:

```json
{
  "Ai": {
    "Provider": "OpenAI"
  }
}
```

3. Run:

```bash
dotnet run
```

> **Note:** The model must support structured outputs (e.g., `gpt-4o`, `gpt-4.1-mini`).

## Tests

```bash
dotnet test
```

Tests cover:
- `FakeFaultAnalyzerTests` — deterministic behaviour, severity mapping, disclaimer, missing-info detection.
- `IndexModelTests` — valid input populates the model; empty input returns a validation error; analyzer exceptions show a friendly error.
- `ProviderSelectionTests` — DI resolves `FakeFaultAnalyzer` for `Ai:Provider=Fake` and `OpenAiFaultAnalyzer` for `Ai:Provider=OpenAI` with valid settings.
- `EvaluationTests` — canned examples scored through the fake provider, asserting symptom recall, severity match, and a "no diagnosis" constraint.

## AI evaluation examples

The evaluation suite in `EvaluationTests.cs` defines 5 hand-written golden examples:

1. Brake pedal noise + soft pedal → expect brake symptom mapping, at least Medium severity.
2. Dashboard warning light + flat battery → expect electrical system, Low severity.
3. Overheating + steam → expect engine/cooling, High severity, DoNotDrive.
4. Rough idle + misfire → expect engine concern, Low severity.
5. Pull to left + clunk over bumps → expect suspension/steering, Low severity.

Scoring weights:
- Symptom recall (40%)
- Severity exact-match (30%)
- No-diagnosis guardrail (30%)

A threshold of `>= 0.6` is used for the fake provider.

## Architecture and design decisions

### Minimal structure

Two projects: `GarageFaultAssistant` (web) and `GarageFaultAssistant.Tests` (xUnit). No layered onion architecture, no repositories, no MediatR, no AutoMapper. One Razor Page handles both input and results.

### Single AI boundary (`IFaultAnalyzer`)

An interface keeps the web layer decoupled from the provider. Two implementations:
- `FakeFaultAnalyzer` — offline demo, deterministic, fully testable.
- `OpenAiFaultAnalyzer` — OpenAI-backed path calling the OpenAI Chat Completions API (JSON schema structured outputs) with an API key.

Provider is chosen at startup via `Ai:Provider`. Default is `Fake` so `dotnet run` works out of the box.

### Structured output

`FaultAnalysis` is a single record with string enums. The JSON schema is generated from the same model to keep the contract in one place. OpenAI returns structured JSON matching the schema, which is deserialized into `FaultAnalysis`. On any parse failure, a safe fallback is returned (original text + `Unknown` severity + disclaimer) instead of throwing to the user.

### No definitive diagnosis

The prompt instructs the model to extract only what is in the text, never invent facts, and phrase `PossibleSystems` as "areas to inspect". The UI explicitly labels this section **"Areas to inspect (not a diagnosis)"**. The `Disclaimer` field is always populated with a fixed safety disclaimer.

### Hallucination mitigations

- Prompt-level guardrails (extract only from provided text, do not invent).
- JSON schema constraints (enums, required fields).
- `NotProvided` field surfaces missing context rather than pretending it exists.
- `Confidence` was removed to keep the MVP minimal; the model's reliability is instead expressed via `NotProvided` and the disclaimer.
- The fake provider is deterministic, so test runs are reproducible and not subject to model drift.
- The OpenAI implementation catches all errors and returns a safe fallback rather than propagating exceptions.

### Secrets

The OpenAI API key and model are read from configuration (User Secrets or environment
variables) via `Ai:OpenAI:ApiKey` and `Ai:OpenAI:Model`. They are never hardcoded or
committed; `appsettings.json` only holds the provider name. A `.gitignore` prevents
committing secrets and build artefacts.

## AI-assisted development

This solution was developed with assistance from an AI coding agent. The agent was used for scaffolding, implementation suggestions, test generation and code review. Its suggestions were reviewed before being accepted, and some were simplified or changed to keep the solution aligned with the time constraint and the take-home requirements.
