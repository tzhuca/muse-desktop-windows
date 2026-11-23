using System.Diagnostics;
using Muse.Mobile.Services;

namespace Muse.Mobile;

public partial class ConversationPage : ContentPage
{
    private readonly GooseApiClient _client;
    private readonly GooseApiClient.ConversationSummary _summary;

    public ConversationPage(GooseApiClient client, GooseApiClient.ConversationSummary summary)
    {
        InitializeComponent();
        _client = client;
        _summary = summary;
        TitleLabel.Text = summary.Title;
        RolePicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadMessagesAsync();
    }

    private async Task LoadMessagesAsync()
    {
        var detail = await _client.GetConversationAsync(_summary.Id);
        if (detail is null)
        {
            return;
        }

        var items = detail.Messages
            .Select(m => $"[{m.Role}] {m.Content}")
            .ToList();

        MessageList.ItemsSource = items;
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var content = InputEntry.Text?.Trim();
        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        var role = RolePicker.SelectedItem?.ToString() ?? "user";
        var message = await _client.AppendMessageAsync(_summary.Id, role, content);
        if (message is null)
        {
            await DisplayAlert("Error", "Failed to send message.", "OK");
            return;
        }

        InputEntry.Text = string.Empty;
        await LoadMessagesAsync();
    }
}
