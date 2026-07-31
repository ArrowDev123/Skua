# Stage 0 — Debug monitoring and performance baseline

## Goal

Build a diagnostic layer before rewriting script/runtime logic. We need to answer “what is slow, allocating, blocking, leaking, or retaining memory?” with evidence from the client and manager.

This stage observes behavior only. It must not change script semantics, scheduling, packet contents, or timing when disabled.

## Operating modes

### Disabled (default)

- No per-packet events.
- No source-code or packet-payload capture.
- No high-frequency polling added by diagnostics.
- Counters use negligible existing lifecycle hooks only.

### Debug monitoring

Opt-in through a developer/debug setting or launch flag. The initial implementation uses `SKUA_DEBUG_MONITORING=1`. Enable bounded runtime snapshots, timings, allocation/GC counters, and structured diagnostics.

### Trace session

Explicit developer action enables higher-detail collection for a bounded duration or file size. Trace sessions must be cancellable and automatically stop on process shutdown.

## Correlation model

Every script execution and diagnostic event must be attributable without exposing credentials:

```text
ProcessId
ClientId                 optional local identifier, never account credentials
RuntimeGenerationId      changes for every loaded runtime generation
RunId                    script execution identity
AttemptId                retry/relogin/restart identity
ScriptIdentity           source kind + stable path/repository identity, not source text
Timestamp                monotonic duration source plus UTC wall-clock for logs
```

Late events from an old generation must be rejected or marked stale. Diagnostic records must never contain account passwords, auth tokens, raw packet payloads, or full script source.

## Measurements

### Process and GC

Capture at a low-frequency interval and at lifecycle boundaries:

- process CPU time and working set;
- managed heap size, committed bytes, fragmentation/LOH/POH information where available;
- allocation rate where supported;
- Gen 0/1/2 collection counts;
- collection pause duration and GC mode;
- thread count, thread-pool busy/available counts, queue length, and starvation indicators;
- exception count by category, without logging sensitive exception payloads.

Use runtime-supported counters/snapshots rather than forcing full collections. A monitoring action must never call `GC.Collect()` in the live client.

### Script pipeline

Record duration and result for:

- source discovery and immutable source snapshot creation;
- include/reference resolution, file count, bytes read, cache identity creation;
- compilation queue wait, compile duration, cache hit/miss, cache rejection reason;
- load-context creation, script configuration, start, stop, and unload result;
- active script task count and task lifetime buckets;
- wait outcome and elapsed time, aggregated by wait category—not every poll;
- relogin attempts, map transitions, retries, and terminal reasons.

### Packet/proxy path

Keep the hot path cheap. Aggregate counters and periodic histograms only:

- packets/bytes in each direction;
- interceptor count and snapshot version;
- processing duration histogram and sampled p95/p99;
- forwarding queue depth, drops, and exceptions;
- active interceptor tasks and shutdown completion.

Never capture packet bodies by default. A separate explicitly enabled trace may record redacted metadata only.

## Implementation shape

Prefer a small internal diagnostics abstraction in `Skua.Core` with adapters for:

- process/runtime counters;
- script lifecycle and compiler timings;
- packet/proxy aggregation;
- structured local event output.

The implementation is `IDiagnosticsService` in `Skua.Core.Interfaces`, `DiagnosticsService` in `Skua.Core`, and bounded diagnostic models in `Skua.Core.Models`. It is registered by common Core services and started/stopped by both Avalonia hosts. It samples every five seconds, retains up to 120 snapshots and 512 events, and is inspectable through the client Diagnostics window when enabled. ScriptManager now emits bounded compile, include-compile, start, stop, and failure events through the same service.

Use monotonic timestamps for durations. Use allocation-free or low-allocation paths for disabled mode. Keep an in-memory bounded ring buffer for the latest diagnostic events and expose a snapshot API for a future debug panel. A rolling local diagnostic file may be added for trace sessions, with size limits and atomic rotation.

The implementation should integrate with existing logging rather than make every subsystem depend on a UI type. Manager and client may consume snapshots independently.

## Tools and outputs

Support standard .NET diagnostics workflows where available:

- live counters for CPU, GC, allocations, thread pool, exceptions, and process memory;
- time-based traces for CPU stacks, blocking, task activity, and GC pauses;
- heap snapshots for suspected retention/AssemblyLoadContext leaks;
- Skua structured snapshots for script generation, cache, wait, relogin, map, and packet summaries.

Document the exact local commands and required build/runtime configuration when the implementation lands. Do not make external telemetry or network uploads part of this stage.

## Guardrails

- Disabled mode has a measured overhead budget and no per-packet allocations.
- Debug mode has a bounded sampling rate, ring-buffer size, and trace file size.
- Every background sampler has cancellation ownership and a terminal task.
- Diagnostics cannot keep a script instance, delegate, timer, task, or collectible load context alive.
- Metrics are aggregated by stable categories; do not use unbounded labels such as full paths, exception messages, or packet strings.
- Counters distinguish “not measured” from zero.
- A diagnostic failure must never fail or stop a script.

## Baseline protocol

Before runtime changes, record a repeatable baseline for:

1. idle client;
2. one repository script;
3. one representative farming script;
4. multiple clients;
5. script compile/cache hit and cache miss;
6. login/relogin and map transition;
7. start/stop/restart cycles;
8. a controlled long-running session.

For each scenario capture CPU, working set, managed heap/GC, thread-pool state, compile/cache timings, script wait totals, packet summaries, and shutdown/unload results. Store summaries without credentials or raw game data.

## Ownership

Stage 0 owns new diagnostics contracts, counters, sinks, sampling policy, baseline scripts/commands, and the debug configuration. Feature tracks emit domain events through that abstraction; they do not each invent their own timers or log formats.

Avoid changing public script-facing interfaces. Do not optimize code in this stage; produce the measurements that justify later changes.

## Acceptance criteria

- Debug monitoring can be enabled without source changes to existing C# scripts.
- Disabled mode adds no per-packet allocations and passes a measured overhead check.
- A trace session can be started, stopped, bounded, and safely written to disk.
- GC, CPU, allocation, thread-pool, exception, compiler, lifecycle, wait, relogin, map, and packet summaries are available.
- A script can be stopped and unloaded with monitoring enabled, with no diagnostic-retained runtime objects.
- Two clients produce distinguishable, non-secret-correlated records.
- Baseline reports can compare before/after runtime changes using the same scenario names.
