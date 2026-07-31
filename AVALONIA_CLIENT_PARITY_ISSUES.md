# Avalonia Client Parity Issues

Date: 2026-02-16

## Startup / Architecture

1. Missing startup side-effects from WPF app startup (high)
- WPF does `FlashTrustManager.EnsureTrustFile()`, creates client directories/files, initializes plugins, reloads hotkeys, and runs exit cleanup.
- Avalonia client currently misses most of this.
- Status: started. Trust/client files/plugin init/hotkey reload/exit cleanup wiring added in `Skua.App.Avalonia/App.axaml.cs`.

2. DI graph for full client app is incomplete (high)
- WPF registers window/dialog/file/dispatcher/hotkey/theme/sound services and `AddSkuaMainAppViewModels()`.
- Avalonia client currently only registers core + flash + main window VM.
- Status: started. Avalonia client now registers platform services and `AddSkuaMainAppViewModels()`; managed window view parity still needs follow-up.

3. Main top menu is static placeholders (high)
- WPF is driven by `MainMenuViewModel` + managed windows + plugin menu updates.
- Avalonia currently hardcodes menu text/items and placeholder Auto/Jump popup content.
- Status: started. Avalonia now builds the top menu from `MainMenuViewModel`, updates plugin items from VM collections, and wires Auto/Jump popup actions to `AutoViewModel`/`JumpViewModel`.

4. Startup handler parity is partial (medium)
- WPF startup handler supports command-line login/server/script/theme/token.
- Avalonia startup handler only handles `requestLoadGame -> loadClient`.
- Status: resolved. Avalonia startup handler now supports `--user`, `--password`, `--server`, `--run-script`, `--use-theme`, and `--gh-token`.

5. Tray behavior parity missing (medium)
- WPF supports tray icon, show/hide, balloon notifications, and tray exit actions.
- Avalonia client does not implement this yet.
- Status: open.

6. SWF source fallback is machine-specific (medium)
- Avalonia currently includes a `FlashTest` fallback path.
- Should move to deterministic build source (AS3 output or explicit artifact path).
- Status: resolved. Avalonia now only copies `skua.swf` from `Skua.AS3\skua\bin\skua.swf`, and shows a specific "Missing game content" error when absent.

7. Manager-launched server login is slow (low)
- Startup now correctly uses the WPF-equivalent `EnsureRelogin(server)` flow.
- The existing retry and login waits add several seconds before the client reaches the selected server.
- Status: open. Reduce unnecessary startup waits without weakening retry behavior.
