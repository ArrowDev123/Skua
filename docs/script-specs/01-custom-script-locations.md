# 01 — Custom script locations and imports

## Goal

Allow users to keep scripts in a configured recursive folder or explicit files, while preserving repository scripts and allowing custom scripts to import both local siblings and supplied core bots.

This work may replace the current include/dependency implementation, but it must preserve existing scripts as a first-class input. The resolver is an internal implementation detail; directive syntax and legacy resolution compatibility are not.

## Current gap

- Only `%AppData%\Skua\Scripts` is enumerated/downloaded.
- `//cs_include` and `//cs_ref` resolve from the default Scripts root, not the file containing the directive.
- Include/dependency identity can collapse files with the same basename.
- `ScriptInfo` is GitHub-oriented and is not a suitable local-file model.

## Design

Persist the two user choices in the shared settings model, with migration-safe defaults:

```text
UserCustomScriptsFolder: string
UserCustomScriptsList: collection of absolute paths
```

Add a local discovery service, separate from `IGetScriptsService`, that:

- recursively enumerates the configured folder;
- includes explicitly registered files;
- normalizes and deduplicates canonical full paths;
- marks stale entries without deleting user files;
- distinguishes `Repository`, `CustomFolder`, and `CustomFile` entries;
- never routes a custom entry through repository delete/reset operations.

Add a pure import resolver. Resolution order:

1. absolute specifier;
2. `./` or `../` relative to the owner file;
3. `Scripts/...` under the supplied Scripts root;
4. for scripts under the legacy repository Scripts root, preserve the existing default-root lookup before adding owner-relative fallback for unqualified names;
5. for custom scripts, resolve other relative paths relative to the owner file, then the supplied Scripts root.

Do not silently change an existing unqualified import when both a legacy-root candidate and an owner-relative candidate exist. Emit a diagnostic for ambiguity and retain the compatibility-selected candidate. New custom scripts should use explicit `./`/`../` paths when they need unambiguous sibling resolution.

Thread `ownerFile` through root, nested include, dependency, and reference processing. Use canonical full paths for the graph and cycle detection. Do not use basenames as identity.

Use separate internal policies for source includes and assembly references. Do not expose a single boolean that lets callers accidentally apply source-file rules to DLL references. Validate managed references and define the allowlist for framework/Skua assemblies.

## Ownership

Primary files:

- new path/source models under `Skua.Core.Models`;
- new `ICustomScriptService` under `Skua.Core.Interfaces` and implementation under `Skua.Core`;
- `Skua.Core/Scripts/ScriptManager.cs` only for resolver integration;
- `Skua.Shared.Avalonia/ViewModels/ScriptRepo/*` and `Controls/ScriptRepo/*` for local entries and commands;
- `Skua.App.Avalonia/ViewModels/ScriptLoaderViewModel.cs` for canonical full-path comparisons.

Do not modify manager update-page files or scheduler files. Coordinate any edits to settings and DI through track 00.

## Acceptance criteria

- A custom root appears recursively in the Script Repo and can load/start a script.
- An explicit file outside that root can load/start without being copied.
- A root script can include a sibling; a nested include can include its own sibling.
- Two equal basenames in different folders resolve correctly.
- `//cs_ref` works from root and nested files.
- Cycles terminate with a useful diagnostic.
- Core supplied scripts remain importable from custom scripts.
- Existing repository scripts compile with the legacy lookup behavior unchanged.
- Existing `Scripts/CoreBots.cs`, `CoreFarms.cs`, and `CoreAdvanced.cs` import forms remain valid.
- Ambiguous legacy imports produce a diagnostic without silently selecting a different file.
- Moving/deleting a custom file produces a stale entry, not silent deletion.
- Repository download/reset behavior is unchanged and never deletes custom files.

## Tests

Track 05 should cover resolver paths, nested includes, duplicate basenames, cycles, settings round-trip, recursive discovery, stale files, repository/custom delete separation, and a legacy-script corpus using every supported include/reference form.

## Risks deliberately left visible

Decide whether unresolved imports are warnings or hard failures. Decide whether custom scripts may import outside configured roots. Do not hide either policy inside path concatenation.
