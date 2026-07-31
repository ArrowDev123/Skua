# 02 — Manager Scripts page

## Goal

Separate script repository maintenance from client updates. The manager should have a dedicated `Scripts` page for refresh/download/reset, while `Updates` handles Velopack only.

## Current gap

`ClientUpdatesViewModel` and `ClientUpdatesUserControl` currently own both Velopack actions and script refresh, bulk download, and reset. The existing Script Repo browser is a separate richer surface and should remain so.

## Design

Create a `ScriptUpdaterViewModel` and view that own:

- catalog refresh;
- missing/outdated downloads;
- reset of the repository Scripts directory and commit marker;
- progress, cancellation, errors, and completion state;
- tray update/reset commands, handled for the singleton lifetime;
- an optional link/open action for the existing Script Repo browser.

Remove script actions and script dependencies from `ClientUpdatesViewModel` and its view. Leave Velopack checking, version display, download/install, and restart there.

The page depends on `IGetScriptsService`, dialog service, and progress/cancellation abstractions. It must not know GitHub URLs or HTTP details.

Reset must be serialized by a shared repository-maintenance service, not only by the view model. Confirm the user action, cancel active repository work using the service contract, remove only the repository Scripts directory and commit marker, recreate the directory, and repopulate through the service. Downloads write to a temporary file and atomically replace verified destinations; cancellation must not leave a valid-looking partial script. Custom script locations from track 01 are never part of reset. A client compilation uses an immutable source snapshot and is not allowed to observe half-written source.

## Ownership

New:

- `Skua.Manager.Avalonia/ViewModels/ScriptUpdaterViewModel.cs`
- `Skua.Manager.Avalonia/UserControls/ScriptUpdaterUserControl.axaml`
- matching code-behind if required by existing Avalonia conventions

Modify:

- manager DI registration;
- `SkuaManager.cs` tab list;
- manager `App.axaml` data template;
- `ClientUpdatesViewModel.cs` and `ClientUpdatesUserControl.axaml` to remove script behavior.

Do not change `GetScriptsService`, shared Script Repo browsing, account launch behavior, or Velopack behavior.

## Acceptance criteria

- Manager shows a dedicated `Scripts` tab.
- `Updates` contains no script actions or script-service dependency.
- Refresh/download/reset work with visible progress and errors.
- Reset removes the repository commit marker and repopulates cleanly.
- Tray update/reset works when another manager tab is selected.
- Switching tabs does not register duplicate message handlers.
- Existing Account Manager → Script Repo behavior still works.
- A failure in script service initialization does not prevent Velopack update checks.
- Existing repository scripts remain loadable after refresh/reset, including scripts that import core bots.

## Tests

Track 05 should verify view-model commands with fakes, service-level reset serialization, atomic download cancellation, message routing while inactive, DI resolution, and Debug/Release builds.

## Non-goals

This page is not the custom script browser, a playlist editor, an Army Control surface, or a replacement for the existing shared Script Repo.
