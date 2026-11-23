using System.Diagnostics;
using Muse.Mobile.Services;

namespace Muse.Mobile;

public partial class SessionsPage : ContentPage
{
    private readonly GooseApiClient _client;

    public SessionsPage(GooseApiClient client)
    {
        InitializeComponent();
        _client = client;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = RefreshConversationsAsync();
    }

    private async void OnNewClicked(object? sender, EventArgs e)
    {
        var created = await _client.CreateConversationAsync(null);
        if (created is not null)
        {
            await RefreshConversationsAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (ConversationList.SelectedItem is not GooseApiClient.ConversationSummary summary)
        {
            await DisplayAlert("Info", "Please select a conversation first.", "OK");
            return;
        }

        var confirm = await DisplayAlert(
            "Confirm",
            $"Delete '{summary.Title}'?",
            "Yes",
            "No");

        if (!confirm)
        {
            return;
        }

        if (await _client.DeleteConversationAsync(summary.Id))
        {
            await RefreshConversationsAsync();
        }
    }

    private async void OnConversationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (ConversationList.SelectedItem is not GooseApiClient.ConversationSummary summary)
        {
            return;
        }

        await Navigation.PushAsync(new ConversationPage(_client, summary));
    }

    private async Task RefreshConversationsAsync()
    {
        try
        {
            var conversations = await _client.GetConversationsAsync();
            ConversationList.ItemsSource = conversations.ToList();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"Could not load conversations: {ex.Message}");
            await DisplayAlert("Error", "Failed to load conversations.", "OK");
        }
    }
}
