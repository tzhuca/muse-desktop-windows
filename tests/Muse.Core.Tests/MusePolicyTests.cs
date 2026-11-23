using Muse.Core;
using Xunit;

namespace Muse.Core.Tests;

public class MusePolicyTests
{
    [Fact]
    public void PermitsMuseAndNarrowlyScopedMetaSignInRedirects()
    {
        Assert.True(MusePolicy.IsTrustedNavigation("https://muse.ai/chat"));
        Assert.True(MusePolicy.IsTrustedNavigation("https://auth.muse.ai/aymh/"));
        Assert.True(MusePolicy.IsTrustedNavigation("https://www.facebook.com/aymh/redirect-cycle"));
        Assert.True(MusePolicy.IsTrustedNavigation("https://www.instagram.com/aymh/redirect-cycle"));
        Assert.True(MusePolicy.IsTrustedNavigation("https://auth.meta.com/aymh/redirect-cycle"));

        Assert.False(MusePolicy.IsTrustedNavigation("https://www.facebook.com/settings"));
        Assert.False(MusePolicy.IsTrustedNavigation("https://muse.ai.evil.example"));
        Assert.False(MusePolicy.IsTrustedNavigation("http://muse.ai"));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com")]
    public void OnlyPermitsHttpAndHttpsExternalLinks(string url)
    {
        Assert.True(MusePolicy.IsAllowedExternalUrl(url));
    }

    [Theory]
    [InlineData("file:///tmp/test")]
    [InlineData("smb://host/share")]
    [InlineData("custom:launch")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a URL")]
    public void RejectsUnsafeExternalSchemes(string url)
    {
        Assert.False(MusePolicy.IsAllowedExternalUrl(url));
    }

    [Fact]
    public void RecognizesTheExactMuseOrigin()
    {
        Assert.True(MusePolicy.IsMuseOrigin("https://muse.ai/chat"));

        foreach (var url in new[]
        {
            "https://auth.muse.ai",
            "https://muse.ai:8443",
            "http://muse.ai",
            "https://muse.ai.evil.test",
            "https://auth.meta.com/aymh/",
            "invalid",
        })
        {
            Assert.False(MusePolicy.IsMuseOrigin(url), url);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("about:blank")]
    [InlineData("https://muse.ai/chat")]
    [InlineData("https://auth.meta.com/aymh/login")]
    [InlineData("https://example.com/oauth/authorize")]
    public void AllowsPopupsForBlankWindowsAndIdentityProviders(string? url)
    {
        Assert.True(MusePolicy.IsAllowedPopupUrl(url));
    }

    [Theory]
    [InlineData("smb://host/share")]
    [InlineData("file:///tmp/test")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,test")]
    [InlineData("custom:launch")]
    [InlineData("invalid")]
    public void DeniesPopupsForUnsafeSchemes(string url)
    {
        Assert.False(MusePolicy.IsAllowedPopupUrl(url));
    }

    [Fact]
    public void BothPermissionKindsAreRestrictedToTheMuseOrigin()
    {
        var permissions = new[]
        {
            MusePermissionKind.Notifications,
            MusePermissionKind.ClipboardSanitizedWrite,
            MusePermissionKind.ClipboardRead,
            MusePermissionKind.ClipboardWrite,
            MusePermissionKind.Media,
            MusePermissionKind.Camera,
            MusePermissionKind.Unknown,
        };

        var urls = new (string? Url, bool Trusted)[]
        {
            ("https://muse.ai", true),
            ("https://muse.ai/chat", true),
            ("https://muse.ai:443/chat", true),
            ("https://auth.muse.ai", false),
            ("https://muse.ai:8443", false),
            ("http://muse.ai", false),
            ("https://muse.ai.evil.test", false),
            ("https://muse.ai@evil.test", false),
            ("https://auth.meta.com/aymh/", false),
            ("https://example.com", false),
            ("file:///tmp/test", false),
            ("about:blank", false),
            ("invalid", false),
            ("", false),
            (null, false),
        };

        foreach (var permission in permissions)
        {
            foreach (var (url, trusted) in urls)
            {
                var expected = trusted
                    && permission is MusePermissionKind.Notifications
                        or MusePermissionKind.ClipboardSanitizedWrite;
                Assert.Equal(expected, MusePolicy.AllowsPermission(permission, url));
            }
        }
    }

    [Theory]
    [InlineData("https://muse.ai/chat", NavigationDecision.Allow)]
    [InlineData("https://auth.meta.com/aymh/login", NavigationDecision.Allow)]
    [InlineData("https://example.com", NavigationDecision.CancelAndOpenExternally)]
    [InlineData("file:///tmp/test", NavigationDecision.Cancel)]
    [InlineData("custom:launch", NavigationDecision.Cancel)]
    public void GuardsMainFrameNavigation(string url, NavigationDecision expected)
    {
        Assert.Equal(expected, MusePolicy.DecideMainFrameNavigation(url));
    }

    [Theory]
    [InlineData("https://example.s.metaaiusercontent.com/", NavigationDecision.Allow)]
    [InlineData("http://example.com/app", NavigationDecision.Allow)]
    [InlineData("https://muse.ai/app", NavigationDecision.Allow)]
    [InlineData("file:///tmp/test", NavigationDecision.Cancel)]
    [InlineData("javascript:alert(1)", NavigationDecision.Cancel)]
    public void AllowsSafeSubframeRedirectsWithoutRelaxingTheTopLevel(string url, NavigationDecision expected)
    {
        Assert.Equal(expected, MusePolicy.DecideSubframeNavigation(url));
    }

    [Theory]
    [InlineData("about:blank", NavigationDecision.Allow)]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth?state=test", NavigationDecision.Allow)]
    [InlineData("https://muse.ai/oauth/callback?code=test", NavigationDecision.Allow)]
    [InlineData("javascript:alert(1)", NavigationDecision.Cancel)]
    [InlineData("file:///tmp/test", NavigationDecision.Cancel)]
    public void GuardsPopupNavigation(string url, NavigationDecision expected)
    {
        Assert.Equal(expected, MusePolicy.DecidePopupNavigation(url));
    }
}
