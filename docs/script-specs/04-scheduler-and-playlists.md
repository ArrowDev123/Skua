# 04 — Scheduler and playlists

## Goal

Provide durable playlists and reliable per-client sequencing without conflating scheduler pause with script termination.

## Current gap

The live path is one `IScriptManager` per client and one loaded script. The manager can launch processes with `--run-script`, but it receives no readiness, completion, failure, or script-status event. `ConcurrentScriptExecutor` is not the scheduler contract.

The scheduler must consume a new trustworthy runtime result, not infer isolation from the existing stopped message. Existing C# scripts remain unaware of scheduler internals.

## State model

```text
Client:   Offline -> Launching -> Ready -> Executing -> Stopping -> Ready/Exited/Faulted
Playlist: Draft -> Ready -> Running <-> Paused -> Stopping -> Completed/Stopped
Item:     Queued -> WaitingForClient -> Starting -> Running
                         -> Completed | Skipped | Failed | Cancelled
```

Initially, `Paused` prevents advancement and new starts. It does not suspend an executing script. A future cooperative script pause can be added as a separate capability.

## Core behavior

- One queue per client, with a higher-level plan assigning items to clients.
- Stable item IDs and canonical script paths.
- Validate existence before changing state.
- Default broken-script behavior: record the failure and skip; make retry/skip/stop configurable.
- Distinguish `ConnectionLost`, relogin started/succeeded/failed, process exit, and script restart. A process exit or relogin restart loses in-memory script state; checkpoint recovery starts a new attempt unless the script explicitly supports checkpoints.
- Stop cancels current work, awaits a terminal event, and persists a checkpoint.
- Persist playlist definition separately from run checkpoint using a schema version and atomic writes.
- Never persist process handles, cancellation tokens, or live view models.
- Start is idempotent; duplicate start/stop requests do not advance the queue twice.

## Required control seam

Define a `ClientAgent`/control adapter with operations equivalent to:

```text
LaunchAsync(client)
WaitReadyAsync()
StartScriptAsync(item)
StopScriptAsync()
GetSnapshot()
events: Ready, Started, Stopped, Failed, Exited
```

Every command/event carries `CommandId`, `RunId`, `ClientId`, `ItemId`, `AttemptId`, runtime generation, timestamp, terminal state, and error details. Use named pipes, localhost IPC, or another authenticated local channel; CLI arguments alone are insufficient for completion-driven scheduling. Credentials and secrets never enter playlist files or IPC logs.

The runtime adapter must expose `Stopped`, `Failed`, or `Leaked`. `Leaked` is terminal for that client process and prevents the scheduler from starting another item there.

## Ownership

Core contracts and serializable DTOs live in `Skua.Core.Interfaces` / `Skua.Core.Models`. Scheduler state and persistence live in `Skua.Core`. The client adapter/control endpoint lives in `Skua.App.Avalonia`; manager process registry, client assignment, and orchestration live in `Skua.Manager.Avalonia`.

Implement core scheduling before manager UI. Keep it independent of Avalonia view models. Do not modify runtime cancellation internals except through the agreed adapter.

## Acceptance criteria

- A playlist can be saved, loaded, reordered, and resumed from a checkpoint.
- Missing/failed scripts are classified and follow the configured policy.
- Pause prevents queue advancement; stop is terminal and bounded.
- A client disconnect does not falsely mark an item complete or claim that execution is resumable.
- A leaked runtime cannot receive another script in the same client process.
- Two clients receive correlated, non-duplicated commands.
- Manager UI shows client/item state and errors from the control channel.

## Tests

Track 05 should test the state machine with fake clients, persistence schema/migration, idempotency, retry/skip/stop policies, disconnect/restart handling, generation and attempt correlation, leaked-runtime blocking, and event correlation. End-to-end IPC and multi-client smoke tests are required before enabling Army-wide automation.

## Deferred VibeSkua-adjacent features

Army broadcast, loadout orchestration, Discord summaries, and in-script pause are separate follow-on specs. They should consume this scheduler/control seam rather than add more global messages.
