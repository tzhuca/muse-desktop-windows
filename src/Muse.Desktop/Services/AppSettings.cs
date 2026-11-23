using System.IO;
using System.Text.Json;

namespace Muse.Desktop.Services;

/// <summary>Reads appsettings.json next to the executable.</summary>
internal sealed class AppSettings
{
    public string GooseApiBaseUrl { get; set; } = "http://localhost:5080/";

    public int GooseApiTimeoutSeconds { get; set; } = 10;

    public int HealthPollSeconds { get; set; } = 30;

    public static AppSettings Load()
    {
        try
        {
            var path = Path.Combine(
                Path.GetDirectoryName(typeof(AppSettings).Assembly.Location)!,
                "appsettings.json");

            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var settings = new AppSettings();

            if (doc.RootElement.TryGetProperty("GooseApi", out var api))
            {
                if (api.TryGetProperty("BaseUrl", out var baseUrl))
                {
                    settings.GooseApiBaseUrl = baseUrl.GetString()!;
                }

                if (api.TryGetProperty("TimeoutSeconds", out var timeout))
                {
                    settings.GooseApiTimeoutSeconds = timeout.GetInt32();
                }

                if (api.TryGetProperty("HealthPollSeconds", out var poll))
                {
                    settings.HealthPollSeconds = poll.GetInt32();
                }
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }
}
