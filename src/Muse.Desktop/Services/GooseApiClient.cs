using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Muse.Desktop.Services;

/// <summary>
/// Client for the local Goose API. Shared HttpClient with JSON camelCase
/// and case-insensitive deserialization.
/// </summary>
public sealed class GooseApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;

    public GooseApiClient(string baseUrl, TimeSpan timeout)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = timeout };
    }

    public async Task<HealthResponse?> GetHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("api/health", ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<HealthResponse>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"GooseApi health check failed: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<ConversationSummary>> GetConversationsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("api/conversations", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<ConversationSummary>>(JsonOptions, ct) ?? [];
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"GetConversations failed: {ex.Message}");
            return [];
        }
    }

    public async Task<ConversationSummary?> CreateConversationAsync(string? title, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("api/conversations", new { title }, JsonOptions, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ConversationSummary>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"CreateConversation failed: {ex.Message}");
            return null;
        }
    }

    public async Task<ConversationDetail?> GetConversationAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"api/conversations/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ConversationDetail>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"GetConversation failed: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeleteConversationAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.DeleteAsync($"api/conversations/{id}", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"DeleteConversation failed: {ex.Message}");
            return false;
        }
    }

    public async Task<MessageDto?> AppendMessageAsync(Guid id, string role, string content, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"api/conversations/{id}/messages",
                new { role, content },
                JsonOptions,
                ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<MessageDto>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"AppendMessage failed: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<SettingDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("api/settings", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<SettingDto>>(JsonOptions, ct) ?? [];
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"GetSettings failed: {ex.Message}");
            return [];
        }
    }

    public async Task<SettingDto?> PutSettingAsync(string key, string value, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.PutAsJsonAsync(
                $"api/settings/{key}",
                new { value },
                JsonOptions,
                ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SettingDto>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"PutSetting failed: {ex.Message}");
            return null;
        }
    }

    // ---- DTOs (mirror Goose.Api.Contracts) ----

    public sealed record HealthResponse(
        string Status,
        string Service,
        string? Database,
        bool DatabaseConnected,
        string? ServerVersion,
        DateTimeOffset Time);

    public sealed record ConversationSummary(
        Guid Id,
        string Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        int MessageCount);

    public sealed record MessageDto(
        Guid Id,
        string Role,
        string Content,
        DateTimeOffset CreatedAt);

    public sealed record ConversationDetail(
        Guid Id,
        string Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<MessageDto> Messages);

    public sealed record SettingDto(
        string Key,
        string Value,
        DateTimeOffset UpdatedAt);
}
