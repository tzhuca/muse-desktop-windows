using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Muse.Desktop;

/// <summary>
/// Keeps one Muse window per session. WebView2 cannot open the same profile
/// from two processes, so a second launch brings the running window forward
/// instead of failing to start.
/// </summary>
internal sealed class SingleInstance
{
    private const string MutexName = @"Local\Muse.Desktop";
    private const int SwRestore = 9;

    private Mutex? _mutex;
    private bool _acquired;

    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            _acquired = createdNew;
            return createdNew;
        }
        catch (Exception ex)
        {
            // A missing or denied mutex must never stop Muse from starting.
            Trace.WriteLine($"Single-instance check unavailable: {ex.Message}");
            _mutex = null;
            _acquired = false;
            return true;
        }
    }

    public void ActivateExisting()
    {
        var currentId = Environment.ProcessId;

        foreach (var process in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))
        {
            if (process.Id == currentId)
            {
                continue;
            }

            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                continue;
            }

            ShowWindow(handle, SwRestore);
            SetForegroundWindow(handle);
            return;
        }
    }

    public void Release()
    {
        if (!_acquired)
        {
            return;
        }

        try
        {
            _mutex?.ReleaseMutex();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not release the instance mutex: {ex.Message}");
        }

        _mutex?.Dispose();
        _mutex = null;
        _acquired = false;
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
