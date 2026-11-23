using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Muse.Desktop;

/// <summary>
/// A sandboxed child window for links that request one: OAuth identity
/// providers and their callbacks. It shares the Muse profile, keeps the opener
/// relationship WebView2 provides, and closes with its opener.
/// </summary>
public partial class PopupWindow : Window
{
    private readonly CoreWebView2Environment _environment;
    private readonly WebView2 _webView;
    private readonly List<Action<CoreWebView2?>> _readyCallbacks = new();
    private CoreWebView2? _core;
    private bool _isReady;

    public PopupWindow(CoreWebView2Environment environment)
    {
        _environment = environment;
        InitializeComponent();
        _webView = WebView;
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Runs <paramref name="callback"/> once this window's web view exists.
    /// The argument is null when initialization failed, so the caller can
    /// cancel the popup instead of leaving a dangling window.
    /// </summary>
    public void WhenWebViewReady(Action<CoreWebView2?> callback)
    {
        if (_isReady)
        {
            callback(_core);
            return;
        }

        _readyCallbacks.Add(callback);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        try
        {
            await _webView.EnsureCoreWebView2Async(_environment);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Popup browser failed to start: {ex.Message}");
            Complete(null);
            return;
        }

        var core = _webView.CoreWebView2;
        if (core is null)
        {
            Complete(null);
            return;
        }

        // Paint the web view before the first frame so popups never flash white.
        _webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(0xFF, 0x10, 0x11, 0x17);

        MuseWindowBehavior.Attach(core, isPopup: true, CreateNestedPopup);
        Complete(core);
    }

    private void Complete(CoreWebView2? core)
    {
        _core = core;
        _isReady = true;

        var callbacks = _readyCallbacks.ToArray();
        _readyCallbacks.Clear();
        foreach (var callback in callbacks)
        {
            callback(core);
        }
    }

    private PopupWindow CreateNestedPopup()
    {
        var popup = new PopupWindow(_environment);
        WindowLink.Link(this, popup);
        popup.Show();
        return popup;
    }
}
