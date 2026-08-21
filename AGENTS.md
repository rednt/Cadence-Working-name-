# AGENTS.md

## Project

Cadence is a .NET 8 time-blocking / daily routine management system. Solution-level build with 5 projects following Clean Architecture.

## Commands

```bash
dotnet build Cadence.sln
dotnet test Cadence.Tests
```

Run a single test class:

```bash
dotnet test Cadence.Tests --filter "FullyQualifiedName~RoutineClockTests"
```

No linter, formatter, or CI config exists. No codegen or migrations yet.

## Solution Layout

| Project | Role | Dependencies |
|---|---|---|
| `Cadence.Core` | Domain models, interfaces, scheduling logic | None |
| `Cadence.Infrastructure` | EF Core SQLite persistence, JSON routine loading | Core |
| `Cadence.Worker` | Background service, RuleEngine host, DI wiring | Core, Infrastructure |
| `Cadence.Cli` | Console entry point (currently stub) | Core, Infrastructure |
| `Cadence.Tests` | xUnit tests | Core, Infrastructure |

All projects target `net8.0` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

## Architecture Rules

- **Cadence.Core has zero external dependencies.** Never add NuGet packages or project references to Core.
- **Infrastructure implements Core interfaces** (`ICadenceStore`, `IRoutineSource`, etc.).
- **Worker and Cli are application shells** that wire up Core + Infrastructure.

## Rule Engine

`RuleEngine` (`Cadence.Core/Scheduling/RuleEngine.cs`) is a stateful class — not a service — that detects block transitions and fires notifications.

- **Cold start:** First `TickAsync()` call snapshots current state silently. No retroactive catch-up for missed transitions.
- **Wake suppression:** Transitions into `BlockRole.Wake` never fire notifications (product decision — waking is biological, not software).
- **Notification flow:** `BlockTransition` always fires on block change. `TaskSurfaced` fires only when pending tasks exist for the active container. `CycleRoll` is logged (not sent) when the day rolls over.
- **State update before side effects:** `_lastBlockLabel` and `_lastCycleId` are updated *before* `SendAsync` to prevent double-fire on crash.
- **Requires `TaskStatus` alias** in any file that also imports async LINQ.

`RuleEngineWorker` (`Cadence.Worker/RuleEngineWorker.cs`) is a thin `BackgroundService` that calls `TickAsync()` every 30 seconds with a try/catch to survive transient failures.

## Domain Model Gotchas

- `RoutineClock` enforces exactly **one Wake block and one Sleep block**. Wake must have the earliest offset from itself (it anchors the day); Sleep must be last. Duplicate start times throw.
- **CycleId rolls at Wake, not midnight.** Past-midnight blocks belong to the previous day's cycle. See `Cadence.Core/Scheduling/RoutineClock.cs:86-94`.
- `TaskStatus` conflicts with `System.Threading.Tasks.TaskStatus`. Any file importing both Core models and async LINQ must alias it: `using TaskStatus = Cadence.Core.Models.TaskStatus;`

## Testing

- Tests use **SQLite in-memory** (`DataSource=:memory:`) via a shared `SqliteConnection`. Each test class creates its own connection and context; no shared fixture.
- `CadenceDbContext.Database.EnsureCreated()` is called in test setup — no migration step required.
- Global `using Xunit;` is declared in `Cadence.Tests.csproj`.

## DI & Testing Patterns

- `SystemClock : IClock` exists for production. Tests use a `FakeClock` that returns a fixed `DateTimeOffset`.
- `RuleEngine` tests use **hand-written mocks** (no Moq). Mock classes are `private sealed` inner classes: `MockRoutineSource`, `MockCadenceStore`, `MockNotificationSender`, `FakeClock`.
- `RuleEngine` is registered as `AddSingleton` (not transient) because it holds mutable state (`_lastBlockLabel`, `_lastCycleId`).
- `INotificationSender` has **no real implementation yet** — the DI container registers it unbound (`AddSingleton<INotificationSender>()`), which will throw at runtime.

## Routine File Format

`JsonRoutineLoader` reads JSON with `{ "profile": "...", "blocks": [...] }`. Block times use 24-hour `HH:mm` format. Enum values are camelCase strings (e.g., `"wake"`, `"sleep"`).

## Naming

The repo directory is `Cadence-Working-name-` (with trailing hyphen). The solution and namespaces use `Cadence` without the suffix.
