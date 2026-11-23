using System.Diagnostics;
using Muse.Mobile.Services;

namespace Muse.Mobile;

public partial class MainPage : ContentPage
{
    private readonly GooseApiClient _apiClient;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _healthCts;

    public MainPage()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _apiClient = new GooseApiClient(
            _settings.GooseApiBaseUrl,
            TimeSpan.FromSeconds(_settings.GooseApiTimeoutSeconds));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadMuse();
        StartHealthPolling();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _healthCts?.Cancel();
    }

    private void LoadMuse()
    {
        try
        {
            MuseWebView.Source = Muse.Core.MusePolicy.MuseUrl;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to load Muse: {ex.Message}");
        }
    }

    private void StartHealthPolling()
    {
        _healthCts?.Cancel();
        _healthCts = new CancellationTokenSource();
        _ = PollHealthAsync(_healthCts.Token);
    }

    private async Task PollHealthAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var health = await _apiClient.GetHealthAsync(ct);
            MainThread.BeginInvokeOnMainThread(() => UpdateStatusBar(health));

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_settings.HealthPollSeconds), ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void UpdateStatusBar(GooseApiClient.HealthResponse? health)
    {
        if (health?.DatabaseConnected == true)
        {
            StatusDot.Fill = Colors.LimeGreen;
            StatusText.Text = "Goose API: connected";
            DbText.Text = $"\u00B7 {health.Database}";
        }
        else if (health is not null)
        {
            StatusDot.Fill = Colors.Orange;
            StatusText.Text = "Goose API: degraded";
            DbText.Text = "";
        }
        else
        {
            StatusDot.Fill = Colors.Gray;
            StatusText.Text = "Goose API: offline";
            DbText.Text = "";
        }
    }

    private async void OnSessionsClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new SessionsPage(_apiClient));
    }
}
