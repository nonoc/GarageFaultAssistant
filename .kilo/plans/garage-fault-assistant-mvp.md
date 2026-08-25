# Garage Fault Assistant — MVP Plan

## Goal

ASP.NET Core Razor Pages app that turns a customer's free-text vehicle fault description
into structured information for a garage service adviser. The AI **must not diagnose**;
it only extracts, classifies and prompts for follow-up. Runs with or without Azure
credentials via a deterministic fake provider.

## Decisions (confirmed)

- Target framework: **.NET 8** (`net8.0`), retargeting both projects from `net10.0`.
- Evaluation: **xUnit tests + README** (no extra page/runner).
- Azure auth: **API key from User Secrets / env vars** (no Entra).

## Principles

- No layered onion/clean-arch scaffolding — one web project + one test project.
- Single interface for the AI boundary; two implementations (Azure / Fake).
- No MediatR, no AutoMapper, no repositories, no EF, no external JSON schema libs.
- Hallucination control is a first-class prompt + schema + UI concern, not an add-on.

## Project layout

```
GarageFaultAssistant/                      (web app, net8.0)
  Program.cs                               DI wiring + provider selection
  appsettings.json                         provider + Azure settings (NO secrets)
  Models/
    FaultAnalysis.cs                       structured output DTO (record)
  Services/
    IFaultAnalyzer.cs                      boundary interface
    AzureOpenAiFaultAnalyzer.cs            Azure.AI.OpenAI impl (JSON schema)
    FakeFaultAnalyzer.cs                   deterministic keyword-based impl
  Pages/
    Index.cshtml / Index.cshtml.cs         form (input) + result panel (same page)
    (keep existing Error/Privacy/Shared)
GarageFaultAssistant.Tests/                (xUnit, net8.0)
  FakeFaultAnalyzerTests.cs
  IndexModelTests.cs
  ProviderSelectionTests.cs
  EvaluationTests.cs                       eval examples + scoring
README.md                                  design decisions + AI-assisted dev + eval
.gitignore                                 dotnet default (secrets never committed)
```

## Structured output — `FaultAnalysis` (Models/FaultAnalysis.cs)

Single record (no inheritance) that the JSON schema is generated from:

| Field | Type | Purpose |
| --- | --- | --- |
| `Summary` | `string` | One-sentence paraphrase of the reported fault |
| `Symptoms` | `string[]` | Symptoms stated by the customer |
| `VehicleContext` | `string?` | Make/model/year/engine **if mentioned** |
| `PossibleSystems` | `string[]` | Systems to investigate (explicitly **not** a diagnosis) |
| `Severity` | `enum` | `Unknown / Low / Medium / High` |
| `Urgency` | `enum` | `Unknown / Routine / BookSoon / DoNotDrive` |
| `SuggestedQuestions` | `string[]` | Info the adviser should gather |
| `NotProvided` | `string[]` | Key facts the customer did not state |
| `Confidence` | `enum` | `Unknown / Low / Medium / High` |
| `Disclaimer` | `string` | Fixed text (always "not a diagnosis") |

Enums are strings in the schema to keep JSON valid and prompt-friendly.

## Services

### `IFaultAnalyzer`
```csharp
Task<FaultAnalysis> AnalyzeAsync(string description, CancellationToken ct = default);
```

### `AzureOpenAiFaultAnalyzer`
- Package: **`Azure.AI.OpenAI`** (latest 2.x).
- `AzureOpenAIClient(endpoint, new ApiKeyCredential(key))` → `ChatClient(deployment)`.
- `CompleteChatAsync` with `ChatResponseFormat.CreateJsonSchemaFormat(schemaName, jsonSchema)`
  (structured outputs). Requires a structured-outputs-capable deployment (e.g. `gpt-4o`).
- **System prompt** enforces: extract only what the customer wrote; never invent vehicle
  details, symptoms or a diagnosis; use `Unknown`/empty arrays when information is absent;
  never produce a definitive cause. `PossibleSystems` is phrased as "areas to inspect".
- Deserialize into `FaultAnalysis`; on any parse failure, return a safe fallback
  (`Summary` = original text, `Confidence = Unknown`, disclaimer intact) instead of throwing.

### `FakeFaultAnalyzer`
- No network. Keyword/rule-based mapping: brake words → brake system, warning light words,
  fluid leaks, noises, severity from urgency keywords (e.g. "smoke", "sparks", "no brakes").
- Always fills `Disclaimer`, always returns deterministic output for the same input
  (makes tests reproducible and acts as the offline demo path).

## Provider selection (`Program.cs`)

- Read `Ai:Provider` (`"Azure"` | `"Fake"`, **default `"Fake"`** so `dotnet run` works with
  zero configuration).
- `Fake` → `IFaultAnalyzer` registered as `FakeFaultAnalyzer` (singleton).
- `Azure` → `AzureOpenAiFaultAnalyzer` registered using `IOptions`-bound `Ai:Azure`
  (Endpoint, Deployment, ApiKey). Registered even if unconfigured, so DI builds but the
  provider throws a clear message at call time if settings are missing.

## Secrets & config

- `appsettings.json` holds **only** `Ai:Provider` and empty placeholder `Ai:Azure:*`.
  No keys, no endpoints with credentials.
- Real values via User Secrets:
  - `dotnet user-secrets init` (in web project)
  - `dotnet user-secrets set "Ai:Azure:Endpoint" "..."`, `"Ai:Azure:Deployment" "..."`,
    `"Ai:Azure:ApiKey" "..."` (or environment variables `Ai__Azure__Endpoint`, etc.)
- Add a **dotnet-default `.gitignore`** (covers `bin/`, `obj/`, user-secrets aren't in repo
  by design; still never commit `appsettings.*.json` containing keys).

## UI (`Pages/Index`)

- Single page: `<textarea>` for the description, a submit button, and a results panel.
- On POST: bind `[BindProperty] string Description`, call the analyzer, assign `FaultAnalysis`
  to the model, re-render with results. Server-side only (no JS fetch — keep it minimal).
- Result panel shows Summary, Symptoms, PossibleSystems (labelled "Areas to inspect — not a
  diagnosis"), Severity, Urgency, SuggestedQuestions, NotProvided, and the Disclaimer.
- Error path: friendly message if the Azure provider is misconfigured.

## Tests (xUnit)

1. `FakeFaultAnalyzerTests` — deterministic: given inputs, assert symptoms/severity/urgency
   mapping and that `Disclaimer` is always populated.
2. `IndexModelTests` — OnPost with a fake analyzer populates the model; validation error on
   empty description.
3. `ProviderSelectionTests` — `Ai:Provider=Fake` resolves `FakeFaultAnalyzer`;
   `=Azure` resolves `AzureOpenAiFaultAnalyzer` (no network touched).
4. `EvaluationTests` — canned examples (description → expected symptoms + severity +
   "no diagnosis" assertion) run through the **fake** provider and scored.

## Simple AI evaluation (`EvaluationTests` + README)

- `EvaluationExample` = `(string description, string[] expectedSymptoms,
  FaultSeverity expectedSeverity)` — 5–6 hand-written golden cases.
- Scoring: symptom recall (intersection/union), severity exact-match, and a
  "no-diagnosis" pass/fail (assert `PossibleSystems` never states a definitive cause).
- Tests assert the fake provider scores ≥ a threshold and that the scorer itself is correct.
- README documents the metric, the examples, and how a human would re-run against Azure.

## README.md

Sections: overview · architecture + design decisions (why one interface, why Fake provider,
why JSON-schema structured output, why no diagnosis) · hallucination mitigations · run with
Fake vs Azure · secrets setup · tests · evaluation approach · AI-assisted development
(what was AI-generated vs human-reviewed, prompting notes).

## Implementation order

1. Add `.gitignore`; retarget both `.csproj` to `net8.0`; add `Azure.AI.OpenAI` package.
2. `Models/FaultAnalysis.cs` (record + enums + JSON-schema generator helper).
3. `IFaultAnalyzer` + `FakeFaultAnalyzer` + `AzureOpenAiFaultAnalyzer`.
4. `Program.cs` DI + provider switch; `appsettings.json` placeholders.
5. `Index` page + code-behind.
6. Tests (4 files above).
7. `README.md`.
8. `dotnet build` + `dotnet test` (verification).
