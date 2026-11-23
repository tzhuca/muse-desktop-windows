namespace Goose.Api.Models;

/// <summary>A conversation the desktop client keeps in the backend.</summary>
public sealed class Conversation
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<Message> Messages { get; set; } = new();
}
