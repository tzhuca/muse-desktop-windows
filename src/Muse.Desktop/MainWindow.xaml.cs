using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Muse.Core;
using Muse.Desktop.Services;

namespace Muse.Desktop;

public partial class MainWindow : Window
{
    private bool _isClosed;
    private GooseApiClient? _apiClient;
    private DispatcherTimer? _healthTimer;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
        WindowStateService.Restore(this);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        var environment = ((App)Application.Current).WebViewEnvironment;
        if (environment is null)
        {
            return;
        }

        try
        {
            await WebView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not start the Muse browser: {ex.Message}");
            MessageBox.Show(
                this,
                "Muse could not start its browser component.\n\nIf Muse is already running, close it and try again.",
                "Muse",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Close();
            return;
        }

        // Paint the web view before the first frame so Muse never flashes white.
        WebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x10, 0x11, 0x17);

        MuseWindowBehavior.Attach(WebView.CoreWebView2!, isPopup: false, CreatePopup);
        _ = LoadMuseWithRecoveryAsync();

        // Start the Goose API health monitor.
        _ = StartApiHealthAsync();
    }

    private void OnClosed(object? sender, System.EventArgs e)
    {
        _isClosed = true;
        _healthTimer?.Stop();
        WindowStateService.Save(this);
    }

    private void OnSessionsClick(object sender, RoutedEventArgs e)
    {
        if (_apiClient is null)
        {
            return;
        }

        var sessions = new SessionsWindow(_apiClient) { Owner = this };
        sessions.Show();
    }

    private async Task StartApiHealthAsync()
    {
        var settings = AppSettings.Load();
        _apiClient = new GooseApiClient(
            settings.GooseApiBaseUrl,
            TimeSpan.FromSeconds(settings.GooseApiTimeoutSeconds));

        await PollHealthAsync();

        _healthTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(settings.HealthPollSeconds),
            DispatcherPriority.Background,
            async (_, _) => await PollHealthAsync(),
            Dispatcher);
        _healthTimer.Start();
    }

    private async Task PollHealthAsync()
    {
        if (_apiClient is null || _isClosed)
        {
            return;
        }

        var health = await _apiClient.GetHealthAsync();
        Dispatcher.Invoke(() => UpdateStatusBar(health));
    }

    private void UpdateStatusBar(GooseApiClient.HealthResponse? health)
    {
        if (_isClosed)
        {
            return;
        }

        if (health?.DatabaseConnected == true)
        {
            StatusDot.Fill = new SolidColorBrush(Colors.LimeGreen);
            StatusText.Text = "Goose API: connected";
            DbText.Text = $"\u00B7 {health.Database}";
        }
        else if (health is not null)
        {
            StatusDot.Fill = new SolidColorBrush(Colors.Orange);
            StatusText.Text = "Goose API: degraded";
            DbText.Text = "";
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Colors.Gray);
            StatusText.Text = "Goose API: offline";
            DbText.Text = "";
        }
    }

    /// <summary>
    /// Loads Muse, and keeps asking the user whether to retry while the first
    /// load keeps failing (offline, captive portal, transient outage).
    /// </summary>
    private async Task LoadMuseWithRecoveryAsync()
    {
        var core = WebView.CoreWebView2;
        if (core is null)
        {
            return;
        }

        while (!_isClosed)
        {
            var loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                core.NavigationCompleted -= OnNavigationCompleted;
                loaded.TrySetResult(e.IsSuccess);
            }

            core.NavigationCompleted += OnNavigationCompleted;
            core.Navigate(MusePolicy.MuseUrl);

            if (await loaded.Task || _isClosed)
            {
                return;
            }

            if (!RecoveryDialog.RequestRetry(this))
            {
                Close();
                return;
            }
        }
    }

    private PopupWindow CreatePopup()
    {
        var environment = ((App)Application.Current).WebViewEnvironment!;
        var popup = new PopupWindow(environment);
        WindowLink.Link(this, popup);
        popup.Show();
        return popup;
    }
}
