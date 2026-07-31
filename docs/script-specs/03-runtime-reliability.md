# 03 — Runtime reliability and performance

## Goal

Make script execution cancellable, bounded, observable, and cheaper without changing the existing script-facing API or normal gameplay semantics. Internal runtime components may be rewritten behind compatibility adapters.

## Safe first tranche

Own these focused fixes:

1. `ScriptWait`: define richer internal outcomes for satisfied, timed out, cancelled, predicate fault, and disconnected. Keep the existing boolean API as a compatibility wrapper until all callers are audited; do not globally change boolean semantics in the first patch. Use monotonic deadlines and one shared wait implementation.
2. `ScriptSend`: remove the nested-task shape from `Task.Factory.StartNew(async ...)`; returned tasks must represent the actual loop.
3. `ScriptAuto`: bind each action to its own CTS and prevent repeated starts from orphaning the previous task.
4. Script-interface delayed callbacks: link delays to runtime cancellation and observe callback failures.
5. Compiler cache: replace unstable 32-bit/string hash identity with a stable versioned manifest and full SHA-256 identity. The manifest includes exact transformed source, a canonically ordered transitive include graph with content digests, resolved reference path/content or assembly identity, effective namespaces, compiler options/language/runtime identity, API assembly identity, and cache format version. Script name is diagnostic metadata unless it changes generated source. Recover from corrupt/old cache entries and never publish failed compilation output.
6. Runtime telemetry: emit through Stage 0’s diagnostics abstraction. Initially limit runtime changes to compilation/cache and lifecycle; add relogin and packet metrics only in their owning tranches. Never add per-packet logging or unbounded labels to the hot path.

Relative import resolution belongs to track 01. Do not duplicate or partially reimplement it here.

## High-risk tranche, separately reviewed

Do not combine these with the safe fixes:

- explicit `Starting/Running/Stopping/Stopped/Failed/Leaked` lifecycle;
- trustworthy terminal results: `Stopped`, `Failed`, or `Leaked`; a leaked runtime blocks another script in that client process;
- generation IDs that prevent late events from an old runtime affecting a new one;
- observable collectible `AssemblyLoadContext` cleanup with bounded `Collected`/`Retained` results;
- single-flight auto-relogin and restart only after confirmed login;
- map timeout/join/cell fallback behavior;
- interceptor snapshots and packet allocation benchmarks;
- removing or wiring the currently unconsumed state-channel paths.

Each needs scenario tests and manual game-client validation. In particular, `StopScript()` currently may interrupt a still-running thread; never claim a successful stop until no script code can issue game calls.

## Ownership

Primary files:

- `Skua.Core/Scripts/ScriptWait.cs`
- `Skua.Core/Scripts/ScriptSend.cs`
- `Skua.Core/Scripts/ScriptAuto.cs`
- targeted lifecycle portions of `ScriptInterface.cs`;
- `Skua.Core/Compiler.cs` and cache code;
- new runtime metrics/contracts where possible.

Avoid include/import portions of `ScriptManager.cs`, manager UI, repository catalog, and scheduler orchestration. Coordinate shared interface/DI edits through track 00.

## Acceptance criteria

- Cancellation during every supported wait returns promptly and never reports success.
- Sending/auto tasks complete only after their actual work stops.
- Delayed callbacks cannot outlive script shutdown.
- A changed included file invalidates the cache even when its timestamp is unchanged.
- Repeated compile/start/stop does not run stale code or retain collectible contexts unexpectedly.
- No new runtime generation starts while the previous one is still stopping.
- Script-owned tasks, callbacks, delegates, timers, event subscriptions, and instances reach terminal state before unload begins.
- Retained collectible contexts are reported; forced GC is not treated as proof of correctness.
- Runtime diagnostics correlate to one script run and remain low-volume.
- Existing C# scripts compile/load through the compatibility facade without source changes.

## Tests

Track 05 should add unit tests for compatibility wrappers, waits, cancellation, task completion, CTS ownership, cache manifests/invalidation/corruption, and metrics. Cache tests must cover changed references, unchanged timestamps, compiler-option changes, path spelling normalization, cross-process same-key compilation, old cache formats, and failed compilation. Manual scenarios are required for legacy script load, stop, relogin, map transitions, and packet throughput.

## Compatibility implementation rule

Prefer a new internal `ScriptRuntime`/lifecycle implementation behind the existing `IScriptManager` and script-facing interfaces. Preserve public signatures and directive behavior first; expose richer outcomes and generation state to the scheduler through new internal/core contracts. Do not force existing compiled scripts to understand cancellation exceptions, scheduler states, or IPC.
