# 05 — Tests and integration

## Goal

Own the validation surface so feature agents can work in parallel without each changing CI, solution wiring, or a shared test project.

## Current state

No dedicated test project was found. Current CI primarily validates restore plus Debug and Release builds. This track owns adding the smallest suitable test project and its CI wiring.

Stage 0 owns the repeatable performance baseline and monitoring smoke checks. This track verifies outputs and overhead; it does not create a competing metrics system.

## Compatibility gate

This is a hard release gate, not optional coverage. Maintain a representative corpus of existing C# scripts and run it against both the compatibility facade and the rewritten runtime path where possible. The corpus must include:

- repository scripts using `Scripts/CoreBots.cs`, `CoreFarms.cs`, and `CoreAdvanced.cs`;
- nested `//cs_include` and `//cs_ref` directives;
- common wait, map, quest, packet, configuration, and relogin calls;
- scripts with options and repeated start/stop;
- duplicate filenames in different directories where resolution must remain deterministic.

The gate checks source compatibility, successful compilation/loading, expected public API calls, and no new source edits required. A failed corpus case blocks enabling the rewrite by default.

## Test layout

Create one test project (for example `Skua.Tests`) targeting the supported .NET target and reference only the layers needed by each test. Keep tests grouped by feature:

- `Scripts/Imports`
- `Scripts/Runtime`
- `Scripts/Scheduler`
- `Manager`

Feature agents provide cases and fakes but do not edit the shared test project or solution after this track claims those files.

## Required coverage

- Import resolver: absolute, owner-relative, default Scripts fallback, nested includes, duplicate basenames, cycles.
- Compatibility: public API surface, legacy directive forms, core-bot imports, configuration/options, and start/stop smoke scripts.
- Local discovery: recursive roots, explicit files, deduplication, stale entries, repository/custom deletion boundaries.
- Runtime: wait cancellation/timeout, task completion, CTS ownership, cache invalidation/corruption, shutdown callback cancellation.
- Runtime isolation: generation ordering, trustworthy terminal result, leaked-runtime blocking, collectible-context retention diagnostics.
- Diagnostics: disabled-mode overhead, bounded sampling/ring buffers, GC/process/thread-pool snapshots, correlation IDs, trace cancellation/rotation, and no retention of collectible runtime objects.
- Manager Scripts page: commands, reset serialization, inactive-tab tray handling, DI resolution, updates-page independence.
- Scheduler: state transitions, persistence, idempotent commands, failure policies, disconnects, event correlation.

## Build and architecture checks

Run:

```powershell
dotnet restore .\Skua.sln
dotnet build .\Skua.sln -c Debug
dotnet build .\Skua.sln -c Release
dotnet test .\Skua.Tests\Skua.Tests.csproj -c Debug
```

Also verify that `Skua.Shared.Avalonia` does not reference App or Manager projects, all new interfaces resolve in both Avalonia composition roots, and repository reset cannot touch configured custom paths.

## Manual smoke matrix

- Load/start/stop a repository script.
- Load/start a custom-root script with nested imports.
- Refresh/download/reset scripts while the manager is on another tab.
- Compile the same script repeatedly, then change an included file without changing its timestamp.
- Disconnect/reconnect during a script and confirm no duplicate relogin or false completion.
- Save/load a playlist, run it with one client, then exercise failure and disconnect paths.
- Run two clients and verify event correlation and no duplicate queue advancement.
- Run the legacy corpus against a clean Scripts directory and a configured custom root.
- Capture the Stage 0 baseline for idle, one script, multiple clients, compile hit/miss, relogin/map transition, and start/stop cycles.

## Merge discipline

The integration agent should not “fix” feature behavior. It reports failures back to the owning spec, keeps CI/build/test files isolated, and preserves unrelated worktree changes.

## Rewrite rollout

Use a compatibility-first rollout:

1. Characterize current behavior and freeze the legacy corpus.
2. Implement the rewritten path behind existing interfaces.
3. Run old and new paths against the corpus where feasible.
4. Enable the new path behind a setting/feature switch for manual validation.
5. Promote only after Debug, Release, unit, compatibility, and multi-client smoke checks pass.

Do not delete the old implementation in the same change that introduces the new runtime. Remove it only after the compatibility gate has passed for a release cycle.
