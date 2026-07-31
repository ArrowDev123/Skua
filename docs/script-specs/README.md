# Script workstreams

These specs split the script work into bounded, agent-ready changes. They are design documents, not a request to copy VibeSkua. VibeSkua is used as a source of behavior to evaluate; the implementation must fit the Avalonia/Core architecture here.

## Hard compatibility requirement

Existing C# scripts must continue to compile, load, and function. Internal runtime code, compiler caching, lifecycle management, import resolution, and orchestration may be rewritten, but the script-facing compatibility surface is a release gate. See the compatibility contract in [00](./00-contracts-and-sequencing.md).

## Recommended order

0. Implement [Stage 0 — Debug monitoring](./00-debug-monitoring.md) and establish a baseline before changing runtime behavior.
1. Read `00-contracts-and-sequencing.md` and agree on the shared seams and compatibility rules.
2. Run track 05 characterization tests before behavior-changing rewrites.
3. Run tracks 01, 02, and 03 in parallel after the contract names are frozen.
4. Run track 04 after lifecycle/IPC contracts exist; its core model can be developed in parallel, but manager integration depends on the client control seam.
5. Run the legacy-script compatibility gate before enabling rewritten runtime paths.

## Worktree rule

The checkout currently contains unrelated pending changes. Agents must not stage, revert, or clean them:

- `Skua.App.Avalonia/Views/GrabberListView.axaml`
- `goals`
- `repos`
- `AVALONIA_CLIENT_PARITY_ISSUES.md`
- `AVALONIA_CLIENT_REVIEW_ACTIONS.md`

There is no dedicated test project today. Track 05 owns creating one, unless the coordinator chooses a different test layout.

## Specs

- [Stage 0 — Debug monitoring and performance baseline](./00-debug-monitoring.md)
- [00 — Contracts and sequencing](./00-contracts-and-sequencing.md)
- [01 — Custom script locations and imports](./01-custom-script-locations.md)
- [02 — Manager Scripts page](./02-manager-scripts-page.md)
- [03 — Runtime reliability and performance](./03-runtime-reliability.md)
- [04 — Scheduler and playlists](./04-scheduler-and-playlists.md)
- [05 — Tests and integration](./05-tests-and-integration.md)
