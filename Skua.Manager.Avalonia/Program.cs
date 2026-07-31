using Avalonia;
using System;
using Velopack;
using Velopack.Locators;
using Velopack.Windows;

namespace Skua.Manager.Avalonia;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnFirstRun(_ => CreateClientShortcuts())
            .OnRestarted(_ => CreateClientShortcuts())
            .Run();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void CreateClientShortcuts()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
#pragma warning disable CS0618 // Velopack auto-manages only the mainExe shortcut; this creates the secondary client shortcut.
            Shortcuts shortcuts = new(VelopackLocator.Current);
            shortcuts.CreateShortcut(
                "Skua.exe",
                ShortcutLocation.Desktop | ShortcutLocation.StartMenuRoot,
                updateOnly: false,
                programArguments: string.Empty,
                icon: null);
#pragma warning restore CS0618
        }
        catch
        {
            // Shortcut creation is optional and must not prevent the manager from starting.
        }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        AppBuilder builder = AppBuilder.Configure<App>()
            .UsePlatformDetect();
#if DEBUG
        builder = builder.WithDeveloperTools();
#endif
        return builder.LogToTrace();
    }
}
