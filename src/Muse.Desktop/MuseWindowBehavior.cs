using System.Diagnostics;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Muse.Core;

namespace Muse.Desktop;

/// <summary>
/// Wires the Muse security policy into a WebView2 instance: navigation guard,
/// managed popups, permission checks, and non-overwriting downloads.
/// Shared by the main window and every sign-in popup.
/// </summary>
internal sealed class MuseWindowBehavior
{
    private readonly Func<PopupWindow> _createPopup;

    private MuseWindowBehavior(Func<PopupWindow> createPopup)
    {
        _createPopup = createPopup;
    }

    public static void Attach(CoreWebView2 core, bool isPopup, Func<PopupWindow> createPopup)
    {
        new MuseWindowBehavior(createPopup).Wire(core, isPopup);
    }

    private void Wire(CoreWebView2 core, bool isPopup)
    {
        // No host objects or web messages: the page gets no .NET surface.
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.IsScriptEnabled = true;
        core.Settings.AreDefaultScriptDialogsEnabled = true;

        core.NavigationStarting += (_, args) =>
        {
            var decision = isPopup
                ? MusePolicy.DecidePopupNavigation(args.Uri)
                : MusePolicy.DecideMainFrameNavigation(args.Uri);
            Apply(decision, args);
        };

        // Hosted apps can redirect a frame to a separate content origin, so
        // subframes allow HTTP(S) without relaxing the top-level allowlist.
        core.FrameNavigationStarting += (_, args) =>
        {
            var decision = isPopup
                ? MusePolicy.DecidePopupNavigation(args.Uri)
                : MusePolicy.DecideSubframeNavigation(args.Uri);
            Apply(decision, args);
        };

        core.NewWindowRequested += (_, args) => HandleNewWindowRequest(args);
        core.PermissionRequested += (_, args) => HandlePermissionRequest(args);
        core.DownloadStarting += (_, args) => HandleDownloadStarting(args);
    }

    private static void Apply(NavigationDecision decision, CoreWebView2NavigationStartingEventArgs args)
    {
        switch (decision)
        {
            case NavigationDecision.Cancel:
                args.Cancel = true;
                break;

            case NavigationDecision.CancelAndOpenExternally:
                args.Cancel = true;
                ExternalLauncher.Open(args.Uri);
                break;
        }
    }

    private void HandleNewWindowRequest(CoreWebView2NewWindowRequestedEventArgs args)
    {
        // OAuth popups and blank-then-redirect windows are allowed; local and
        // custom schemes never are.
        if (!MusePolicy.IsAllowedPopupUrl(args.Uri))
        {
            args.Handled = true;
            return;
        }

        // The host creates the window, and WebView2 keeps window.opener,
        // postMessage, and POST bodies intact by filling the one we hand back.
        args.Handled = true;
        var deferral = args.GetDeferral();
        var settled = false;

        void Settle(Action action)
        {
            if (settled)
            {
                return;
            }

            settled = true;
            try
            {
                action();
            }
            finally
            {
                deferral.Complete();
            }
        }

        try
        {
            var popup = _createPopup();
            popup.WhenWebViewReady(core =>
            {
                if (core is null)
                {
                    Settle(() => Trace.WriteLine("Popup web view failed to initialize"));
                    return;
                }

                Settle(() =>
                {
                    try
                    {
                        args.NewWindow = core;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Could not host popup: {ex.Message}");
                    }
                });
            });
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not create popup: {ex.Message}");
            Settle(() => { });
        }
    }

    private static void HandlePermissionRequest(CoreWebView2PermissionRequestedEventArgs args)
    {
        var kind = args.PermissionKind switch
        {
            CoreWebView2PermissionKind.Notifications => MusePermissionKind.Notifications,
            CoreWebView2PermissionKind.ClipboardRead => MusePermissionKind.ClipboardRead,
            CoreWebView2PermissionKind.Camera => MusePermissionKind.Camera,
            CoreWebView2PermissionKind.Microphone => MusePermissionKind.Media,
            _ => MusePermissionKind.Unknown,
        };

        // Clipboard writes from a user gesture are handled by Chromium itself;
        // programmatic clipboard reads stay denied for every origin.
        args.State = MusePolicy.AllowsPermission(kind, args.Uri)
            ? CoreWebView2PermissionState.Allow
            : CoreWebView2PermissionState.Deny;
    }

    private static void HandleDownloadStarting(CoreWebView2DownloadStartingEventArgs args)
    {
        args.Handled = true;

        string? destination = null;
        try
        {
            var fileName = GuessFileName(args.ResultFilePath, args.DownloadOperation?.Uri);
            destination = DownloadPath.Reserve(DownloadPath.DefaultDownloadsDirectory(), fileName);
            args.ResultFilePath = destination;

            if (args.DownloadOperation is { } operation)
            {
                WatchDownload(operation, destination);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not reserve download destination: {ex.Message}");
            if (destination is not null)
            {
                TryDelete(destination);
            }

            args.Cancel = true;
        }
    }

    private static void WatchDownload(CoreWebView2DownloadOperation operation, string destination)
    {
        void OnStateChanged(object? sender, object? state)
        {
            switch (operation.State)
            {
                case CoreWebView2DownloadState.Completed:
                    operation.StateChanged -= OnStateChanged;
                    break;

                case CoreWebView2DownloadState.Interrupted:
                    operation.StateChanged -= OnStateChanged;
                    TryDelete(destination);
                    break;
            }
        }

        operation.StateChanged += OnStateChanged;
    }

    private static string? GuessFileName(string? resultFilePath, string? downloadUri)
    {
        var fileName = Path.GetFileName(resultFilePath ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        if (Uri.TryCreate(downloadUri, UriKind.Absolute, out var uri))
        {
            fileName = Path.GetFileName(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return null;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not remove {path}: {ex.Message}");
        }
    }
}
