using System.ComponentModel.DataAnnotations;

namespace Goose.Api.Contracts;

/// <summary>Service and database reachability, used by the desktop status bar.</summary>
public sealed record HealthResponse(
    string Status,
    string Service,
    string? Database,
    bool DatabaseConnected,
    string? ServerVersion,
    DateTimeOffset Time);

public sealed record ConversationSummaryDto(
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

public sealed record ConversationDetailDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MessageDto> Messages);

public sealed class CreateConversationRequest
{
    [MaxLength(200)]
    public string? Title { get; set; }
}

public sealed class RenameConversationRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
}

public sealed class AppendMessageRequest
{
    [Required]
    [MaxLength(32)]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}

public sealed record SettingDto(
    string Key,
    string Value,
    DateTimeOffset UpdatedAt);

public sealed class UpsertSettingRequest
{
    [Required]
    public string Value { get; set; } = string.Empty;
}
