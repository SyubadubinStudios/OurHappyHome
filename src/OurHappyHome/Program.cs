using Avalonia;

namespace OurHappyHome;

internal static class Program
{
    /// <summary>Command line: <c>--screenshots &lt;folder&gt;</c> captures the documentation screenshots and exits.</summary>
    public static string? ScreenshotFolder { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        int index = Array.IndexOf(args, "--screenshots");
        if (index >= 0 && index + 1 < args.Length)
        {
            ScreenshotFolder = Path.GetFullPath(args[index + 1]);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
