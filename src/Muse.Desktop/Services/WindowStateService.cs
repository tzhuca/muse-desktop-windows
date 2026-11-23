using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace Muse.Desktop.Services;

/// <summary>
/// Saves and restores the main window's position, size, and state
/// so it reopens where the user left it.
/// </summary>
public static class WindowStateService
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Muse",
        "window-state.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Save(Window window)
    {
        try
        {
            var state = new WindowState
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height,
                IsMaximized = window.WindowState == System.Windows.WindowState.Maximized,
            };

            var dir = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(StatePath, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to save window state: {ex.Message}");
        }
    }

    public static void Restore(Window window)
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return;
            }

            var json = File.ReadAllText(StatePath);
            var state = JsonSerializer.Deserialize<WindowState>(json, JsonOptions);
            if (state is null)
            {
                return;
            }

            // Ensure the saved rectangle is at least partially visible on the current screen setup.
            var virtualScreen = new Rect(
                SystemParameters.VirtualScreenLeft,
                SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth,
                SystemParameters.VirtualScreenHeight);
            var saved = new Rect(state.Left, state.Top, state.Width, state.Height);

            if (!virtualScreen.IntersectsWith(saved))
            {
                // Saved position is off every screen; keep the default centered placement.
                return;
            }

            window.Left = state.Left;
            window.Top = state.Top;
            window.Width = state.Width;
            window.Height = state.Height;

            if (state.IsMaximized)
            {
                window.WindowState = System.Windows.WindowState.Maximized;
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Failed to restore window state: {ex.Message}");
        }
    }

    private sealed class WindowState
    {
        [JsonPropertyName("left")]
        public double Left { get; set; }

        [JsonPropertyName("top")]
        public double Top { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }

        [JsonPropertyName("isMaximized")]
        public bool IsMaximized { get; set; }
    }
}
