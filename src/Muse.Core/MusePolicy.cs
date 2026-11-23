using System.Diagnostics.CodeAnalysis;

namespace Muse.Core;

/// <summary>Permission names, mirroring the Electron/Chromium permission strings.</summary>
public enum MusePermissionKind
{
    Notifications,
    ClipboardSanitizedWrite,
    ClipboardRead,
    ClipboardWrite,
    Media,
    Camera,
    Unknown,
}

/// <summary>What a web view should do when a navigation is requested.</summary>
public enum NavigationDecision
{
    /// <summary>Let the web view navigate.</summary>
    Allow,

    /// <summary>Block the navigation and stay where we are.</summary>
    Cancel,

    /// <summary>Block the navigation and hand the URL to the system browser.</summary>
    CancelAndOpenExternally,
}

/// <summary>
/// The Muse security policy: which URLs may live inside the app window, which
/// leave for the system browser, and which permissions Muse may request.
/// This is the C# port of electron/utils.cjs.
/// </summary>
public static class MusePolicy
{
    /// <summary>The page the app loads on startup.</summary>
    public const string MuseUrl = "https://muse.ai/";

    /// <summary>The only origin allowed to request permissions.</summary>
    public const string MuseOrigin = "https://muse.ai";

    /// <summary>Meta-hosted sign-in redirect handlers Muse bounces through.</summary>
    private static readonly HashSet<string> MetaRedirectHosts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "www.facebook.com",
            "www.instagram.com",
            "auth.meta.com",
        };

    /// <summary>
    /// True when the main window may navigate to <paramref name="rawUrl"/> in place:
    /// Muse itself, its subdomains, or a narrow Meta sign-in redirect.
    /// </summary>
    public static bool IsTrustedNavigation(string? rawUrl)
    {
        if (!TryParse(rawUrl, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return false;
        }

        var host = uri.Host;
        if (host == "muse.ai" || host.EndsWith(".muse.ai", StringComparison.Ordinal))
        {
            return true;
        }

        return MetaRedirectHosts.Contains(host)
            && uri.AbsolutePath.StartsWith("/aymh/", StringComparison.Ordinal);
    }

    /// <summary>True for plain HTTP(S) links, which are safe to hand to the system browser.</summary>
    public static bool IsAllowedExternalUrl(string? rawUrl)
    {
        if (!TryParse(rawUrl, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when <paramref name="rawUrl"/> is exactly the Muse origin: no other
    /// host, subdomain, port, or scheme qualifies.
    /// </summary>
    public static bool IsMuseOrigin(string? rawUrl)
    {
        return TryParse(rawUrl, out var uri)
            && string.Equals(GetOrigin(uri), MuseOrigin, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when a new window may be created for <paramref name="url"/>. Blank
    /// windows are allowed because OAuth flows open one and then navigate it
    /// asynchronously; identity providers still have to be HTTP(S).
    /// </summary>
    public static bool IsAllowedPopupUrl(string? url)
    {
        return string.IsNullOrEmpty(url)
            || string.Equals(url, "about:blank", StringComparison.Ordinal)
            || IsAllowedExternalUrl(url);
    }

    /// <summary>
    /// Only Muse's own origin may show notifications or write to the clipboard.
    /// Programmatic clipboard reads stay denied everywhere.
    /// </summary>
    public static bool AllowsPermission(MusePermissionKind kind, string? requestingUrl)
    {
        var allowed = kind is MusePermissionKind.Notifications
            or MusePermissionKind.ClipboardSanitizedWrite;
        return allowed && IsMuseOrigin(requestingUrl);
    }

    /// <summary>Main-frame policy for the Muse window.</summary>
    public static NavigationDecision DecideMainFrameNavigation(string? url)
    {
        if (IsTrustedNavigation(url))
        {
            return NavigationDecision.Allow;
        }

        return IsAllowedExternalUrl(url)
            ? NavigationDecision.CancelAndOpenExternally
            : NavigationDecision.Cancel;
    }

    /// <summary>
    /// Policy for the managed authentication popups: they may visit identity
    /// providers and callback URLs, but never local or custom schemes.
    /// </summary>
    public static NavigationDecision DecidePopupNavigation(string? url)
    {
        return IsAllowedPopupUrl(url) ? NavigationDecision.Allow : NavigationDecision.Cancel;
    }

    /// <summary>
    /// Policy for iframes inside the Muse window: hosted apps may redirect to a
    /// separate content origin, so allow any HTTP(S) there without relaxing the
    /// top-level allowlist.
    /// </summary>
    public static NavigationDecision DecideSubframeNavigation(string? url)
    {
        return IsAllowedExternalUrl(url) ? NavigationDecision.Allow : NavigationDecision.Cancel;
    }

    /// <summary>
    /// Scheme://host[:port] with default ports and userinfo omitted, matching
    /// JavaScript's URL.origin. Uri.Authority is the only component that drops
    /// the implicit port: https://muse.ai/chat is "https://muse.ai", while
    /// https://muse.ai:8443 stays "https://muse.ai:8443".
    /// </summary>
    public static string GetOrigin(Uri uri)
    {
        return $"{uri.Scheme}://{uri.Authority}";
    }

    private static bool TryParse(string? rawUrl, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return false;
        }

        return Uri.TryCreate(rawUrl, UriKind.Absolute, out uri);
    }
}
