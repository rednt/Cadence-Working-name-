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
| `Cadence.Cli` | Interactive CLI (`status`, `add`, `complete`) | Core, Infrastructure |
| `Cadence.Tests` | xUnit tests | Core, Infrastructure |

All projects target `net8.0` with `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

## Architecture Rules

- **Cadence.Core has zero external dependencies.** Never add NuGet packages or project references to Core.
- **Infrastructure implements Core interfaces** (`ICadenceStore`, `IRoutineSource`, etc.).
- **Worker and Cli are application shells** that wire up Core + Infrastructure.


## DI Lifetime Rationale

- **`RuleEngine` is singleton** because it holds mutable state (`_lastBlockLabel`, `_lastCycleId`, `_initialized`) that must survive across ticks. If scoped, each tick gets a fresh instance and the `_initialized` flag resets — block transitions are never detected.
- **`CadenceDbContext` is singleton** because SQLite is a file-based, single-writer database. No client-server connection pool, no concurrent scope conflicts. This is safe for a single-threaded background worker + CLI.
- **`ICadenceStore` is singleton** to match the DbContext lifetime it wraps.
- **Migration path:** If Cadence ever becomes a web app or multi-threaded service, switch `RuleEngine` to scoped and inject `IServiceScopeFactory` to resolve `ICadenceStore` per tick. The constructor signature does not change — only the DI registration and worker scope management change.


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
- `INotificationSender` is implemented by `ConsoleNotificationSender` (`Cadence.Infrastructure/Notifications/NotificationSender.cs`). Registered as singleton.


## Routine File Format

`JsonRoutineLoader` reads JSON with `{ "profile": "...", "blocks": [...] }`. Block times use 24-hour `HH:mm` format. Enum values are camelCase strings (e.g., `"wake"`, `"sleep"`).




## Config vs State Separation

- **`routines/default.json`** is Configuration as Code — edited in VS Code, version-controlled, defines the block schedule (times, labels, roles). Never edited by the CLI at runtime.
- **`CadenceDB/cadence.db`** is mutable runtime state — tasks, notification logs. The CLI writes here only.
- **CLI never touches config in v1.** Hot-reload (`IOptionsMonitor` / `FileSystemWatcher`) is deferred to a future version.

## CLI Command Surface

| Command | Syntax | Description |
|---|---|---|
| `status` | `status` | Show current block, cycle ID, and pending tasks |
| `add` | `add "Title" --container "Label"` | Add a task (defaults to current block if no `--container`) |
| `complete` | `complete [Id]` | Mark task as completed; shows pending tasks if no ID given |
| `modify` | `modify [Id] "New Title"` | Modify a task's title; shows tasks if no ID given |
| `containers` | `containers` | List all blocks with pending counts + orphan detection |

Planned (Day 8+): worker liveness heartbeat.

## Containers Command Design

The `containers` command merges two data sources:
- **Blocks** come from `IRoutineSource.Blocks` (in-memory, loaded from `default.json` at startup)
- **Task counts** come from `ICadenceStore.GetContainerTaskCountsAsync()` (one SQL `GROUP BY` query)

Blocks are **never** inserted into SQLite. They are Configuration as Code — the JSON file is the single source of truth. The CLI joins the two sources in memory to produce the output. Orphan detection finds task `ContainerLabel` values that don't match any block in the routine.

## Worker Liveness Pattern

`RuleEngineWorker` logs `"Cadence Rule Engine started"` on startup. A future heartbeat will write a `Heartbeat` record to SQLite every tick, allowing an external monitor (Kubernetes liveness probe, health check endpoint) to verify the worker is alive. If no heartbeat within `2 × Interval`, the worker is considered dead.

## Naming

The repo directory is `Cadence-Working-name-` (with trailing hyphen). The solution and namespaces use `Cadence` without the suffix.

