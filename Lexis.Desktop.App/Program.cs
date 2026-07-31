using Avalonia;
using System;
using System.Runtime;

namespace Lexis.Desktop.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Prefer low-latency GC during desk lifetime (scheda §2.1).
        try { GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency; }
        catch { /* ignore on constrained runtimes */ }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // DeveloperTools disabled: left the main window invisible/stuck here.
            .WithInterFont()
            .LogToTrace();
}
