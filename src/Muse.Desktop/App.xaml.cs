using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Muse.Desktop;

public partial class App : Application
{
    /// <summary>
    /// One WebView2 environment for the whole process, so the main window and
    /// its sign-in popups share a single Muse profile (cookies, storage).
    /// </summary>
    internal CoreWebView2Environment? WebViewEnvironment { get; private set; }

    private SingleInstance? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new SingleInstance();
        if (!_singleInstance.TryAcquire())
        {
            _singleInstance.ActivateExisting();
            Shutdown();
            return;
        }

        if (!TryCreateEnvironment())
        {
            Shutdown();
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private bool TryCreateEnvironment()
    {
        try
        {
            // Surfaces WebView2RuntimeNotFoundException when the Evergreen
            // Runtime is missing, so we can explain instead of crashing.
            _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"WebView2 runtime check failed: {ex.Message}");
            MessageBox.Show(
                "Muse needs the Microsoft Edge WebView2 Runtime, which is not installed.\n\n" +
                "Install it from https://developer.microsoft.com/microsoft-edge/webview2/ and start Muse again.",
                "Muse",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }

        // Keep the browser profile in a predictable, product-specific directory.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Muse");

        try
        {
            WebViewEnvironment = CoreWebView2Environment
                .CreateAsync(userDataFolder: userDataFolder)
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"WebView2 environment failed: {ex.Message}");
            MessageBox.Show(
                "Muse could not start its browser profile.\n\n" +
                "If Muse is already running, close it and try again.",
                "Muse",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Release();
        base.OnExit(e);
    }
}
