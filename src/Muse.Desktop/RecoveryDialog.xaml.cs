using System.Windows;

namespace Muse.Desktop;

/// <summary>
/// Shown when Muse cannot be reached. Returns true for "Retry" and false for
/// "Close"; closing the window the platform way counts as "Close".
/// </summary>
public partial class RecoveryDialog : Window
{
    public RecoveryDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => RetryButton.Focus();
    }

    public static bool RequestRetry(Window owner)
    {
        var dialog = new RecoveryDialog { Owner = owner };
        return dialog.ShowDialog() == true;
    }

    private void OnRetryClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
