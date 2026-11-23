using System.Reflection;
using System.Text.Json;

namespace Muse.Mobile.Services;

/// <summary>
/// Reads appsettings.json bundled as an embedded resource.
/// </summary>
public sealed class AppSettings
{
    public string GooseApiBaseUrl { get; set; } = "http://127.0.0.1:5080/";
    public int GooseApiTimeoutSeconds { get; set; } = 10;
    public int HealthPollSeconds { get; set; } = 30;

    public static AppSettings Load()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Muse.Mobile.appsettings.json");
            if (stream is null)
            {
                return new AppSettings();
            }

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var doc = JsonDocument.Parse(json);

            var settings = new AppSettings();
            if (doc.RootElement.TryGetProperty("GooseApi", out var api))
            {
                if (api.TryGetProperty("BaseUrl", out var baseUrl))
                    settings.GooseApiBaseUrl = baseUrl.GetString() ?? settings.GooseApiBaseUrl;
                if (api.TryGetProperty("TimeoutSeconds", out var timeout))
                    settings.GooseApiTimeoutSeconds = timeout.GetInt32();
                if (api.TryGetProperty("HealthPollSeconds", out var poll))
                    settings.HealthPollSeconds = poll.GetInt32();
            }

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }
}
