namespace Goose.Api.Models;

/// <summary>A key/value pair the desktop clients sync through the API.</summary>
public sealed class Setting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
