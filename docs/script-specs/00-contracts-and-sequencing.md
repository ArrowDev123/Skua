# 00 — Contracts and sequencing

## Purpose

Freeze the small shared vocabulary before parallel implementation starts. This track owns contracts only; it should avoid feature behavior and UI. Internal implementations may be replaced, but the existing script-facing contract must remain stable.

## Compatibility contract

The following are compatibility surface and may not be removed or renamed without a deliberate versioning plan:

- Public script-facing namespaces, types, methods, properties, overloads, enums, and message/configuration shapes exposed through `IScriptInterface` and related interfaces.
- Existing `//cs_include` and `//cs_ref` directive syntax, `Scripts/...` paths, repository-relative paths, and core-bot imports.
- Existing script configuration loading and option persistence.
- Normal success behavior of waits, map joins, quest/packet calls, relogin, and script start/stop. Bug fixes may change incorrect failure behavior only when the compatibility test identifies the old behavior as accidental and the new behavior is documented.
- The ability to compile and execute existing repository scripts without requiring source edits.

Compatibility should be maintained through a stable facade/adapter. Rewritten internals must not leak new lifecycle, scheduler, or cache types into compiled scripts unless they are additive.

Required release gate: a representative legacy C# script corpus must compile and load against the rewritten runtime, and smoke scripts must exercise core bots, includes, waits, configuration, map/quest APIs, and stop/restart paths.

## Required invariants

- Runtime script paths are normalized absolute paths.
- Repository metadata paths remain repository-relative and continue to resolve under `ClientFileSources.SkuaScriptsDIR`.
- Existing default-root scripts retain their legacy import fallback behavior; new owner-relative resolution is additive and must not make an existing unqualified import resolve to an incompatible file.
- Repository download/update/delete stays in `IGetScriptsService`; local discovery is a separate service.
- Manager-to-client commands are process-boundary operations. In-process messenger messages are not a manager control protocol.
- Scheduler events carry `CommandId`, `RunId`, `ClientId`, `ItemId`, `AttemptId`, runtime generation, timestamp, terminal state, and error details.
- Pause initially means “do not advance or start new work.” It must not pretend that `StopScript()` preserves execution state.
- Script reset must be serialized against downloads and must not delete custom files.
- Only one script execution generation may issue game calls per client.
- Every asynchronous operation has a clear owner, lifetime, and observed terminal result.
- A compilation uses one immutable source/dependency snapshot.
- Cache identity is content-based and versioned.
- A rewritten implementation is never enabled globally until the legacy-script compatibility gate passes.
- Compatibility failures identify the script, directive/API surface, runtime generation, and first differing result.

## Proposed shared types

Names are proposals; settle them before implementation:

```csharp
enum ScriptSourceKind { Repository, CustomFolder, CustomFile }

record ScriptEntry(
    string FullPath,
    string DisplayName,
    ScriptSourceKind Source,
    IReadOnlyList<string> Tags,
    bool CanDelete);

interface IScriptSourceResolver
{
    bool TryResolve(string specifier, string ownerFile, out string resolvedPath);
}

interface IAssemblyReferenceResolver
{
    bool TryResolve(string specifier, string ownerFile, out string resolvedPath);
}
```

For scheduling, use serializable DTOs rather than view models:

```text
PlaylistItem: Id, ScriptPath, DisplayName, OptionsProfile, TargetClient, FailurePolicy
PlaylistDefinition: SchemaVersion, Name, Items
RunCheckpoint: RunId, PlaylistId, CurrentItemId, State, LastError
```

## Single-owner files

Only one agent may edit these shared hotspots in a batch:

- `Skua.Core.Models/SettingsModels.cs`
- `Skua.Core.Models/ClientFileSources.cs`
- `Skua.Core.Interfaces/Scripts/IScriptInterface.cs`
- `Skua.Core.Interfaces/Scripts/Manager/IScriptManager.cs`
- `Skua.Core/Scripts/ScriptInterface.cs`
- `Skua.Core/Scripts/ScriptManager.cs`
- `Skua.Core/Services/UnifiedSettingsService.cs`
- `Skua.App.Avalonia/AppStartup/Services.cs`
- `Skua.Manager.Avalonia/AppStartup/Services.cs`

Prefer new contracts and adapters over widening these files. If a signature must change, the contract owner records every caller before another agent starts.

## Dependency order

```text
debug monitoring/baseline
       |
contracts/models
       |
       +--> custom roots/imports ----+
       +--> manager Scripts page     +--> integration/validation
       +--> runtime reliability -----+
       |
       +--> scheduler core --> IPC/client control --> manager orchestration
```

The first three feature tracks may work in parallel once this document’s invariants are accepted. Scheduler manager integration waits for a correlated client control channel.

## Non-goals

Stage 0 establishes the baseline before behavior-changing work. Scheduler manager integration also waits for trustworthy runtime lifecycle results, not only the IPC channel.

- No WPF code or legacy installer work.
- No direct port of VibeSkua view models.
- No change to Velopack channels.
- No broad rewrite of `IScriptInterface`.
