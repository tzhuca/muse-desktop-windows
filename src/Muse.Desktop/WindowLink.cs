using System.Windows;

namespace Muse.Desktop;

/// <summary>
/// Ties a popup's lifetime to its opener: authentication windows never
/// outlive the window that opened them, and closing one releases the other.
/// </summary>
internal static class WindowLink
{
    public static void Link(Window parent, Window child)
    {
        var childClosed = false;

        void OnParentClosed(object? sender, System.EventArgs e)
        {
            if (!childClosed)
            {
                child.Close();
            }
        }

        void OnChildClosed(object? sender, System.EventArgs e)
        {
            childClosed = true;
            parent.Closed -= OnParentClosed;
        }

        parent.Closed += OnParentClosed;
        child.Closed += OnChildClosed;
    }
}
