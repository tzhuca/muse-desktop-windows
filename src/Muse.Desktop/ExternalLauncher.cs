using System.Diagnostics;
using Muse.Core;

namespace Muse.Desktop;

/// <summary>Hands links the app refuses to host to the user's default browser.</summary>
internal static class ExternalLauncher
{
    public static void Open(string? url)
    {
        if (!MusePolicy.IsAllowedExternalUrl(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not open external link: {ex.Message}");
        }
    }
}
