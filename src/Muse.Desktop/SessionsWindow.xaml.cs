using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Muse.Desktop.Services;

namespace Muse.Desktop;

/// <summary>
/// Lists conversations from the Goose API, shows messages, and lets the user
/// create, delete, and append messages.
/// </summary>
public partial class SessionsWindow : Window
{
    private readonly GooseApiClient _client;

    public SessionsWindow(GooseApiClient client)
    {
        _client = client;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await RefreshConversationsAsync();
    }

    private async void OnNewClick(object sender, RoutedEventArgs e)
    {
        var created = await _client.CreateConversationAsync(null);
        if (created is not null)
        {
            await RefreshConversationsAsync();
            ConversationList.SelectedItem = created;
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ConversationList.SelectedItem is not GooseApiClient.ConversationSummary summary)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Delete '{summary.Title}'?",
            "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        if (await _client.DeleteConversationAsync(summary.Id))
        {
            await RefreshConversationsAsync();
            MessageList.Items.Clear();
            DetailTitle.Text = "Select a conversation";
        }
    }

    private async void OnConversationSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ConversationList.SelectedItem is not GooseApiClient.ConversationSummary summary)
        {
            return;
        }

        var detail = await _client.GetConversationAsync(summary.Id);
        if (detail is null)
        {
            return;
        }

        DetailTitle.Text = detail.Title;
        MessageList.Items.Clear();
        foreach (var message in detail.Messages)
        {
            MessageList.Items.Add($"[{message.Role}] {message.Content}");
        }
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        await SendAsync();
    }

    private async void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SendAsync();
        }
    }

    private async Task SendAsync()
    {
        if (ConversationList.SelectedItem is not GooseApiClient.ConversationSummary summary)
        {
            return;
        }

        var content = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        var role = (RoleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "user";
        var message = await _client.AppendMessageAsync(summary.Id, role, content);
        if (message is null)
        {
            return;
        }

        InputBox.Clear();
        MessageList.Items.Add($"[{message.Role}] {message.Content}");
        MessageList.ScrollIntoView(MessageList.Items[MessageList.Items.Count - 1]);
    }

    private async Task RefreshConversationsAsync()
    {
        try
        {
            var conversations = await _client.GetConversationsAsync();
            ConversationList.Items.Clear();
            foreach (var c in conversations)
            {
                ConversationList.Items.Add(c);
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not load conversations: {ex.Message}");
        }
    }
}
