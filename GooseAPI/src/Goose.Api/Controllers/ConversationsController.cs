using Goose.Api.Contracts;
using Goose.Api.Data;
using Goose.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Goose.Api.Controllers;

/// <summary>Conversations and their messages, persisted in SQL Server.</summary>
[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController : ControllerBase
{
    private static readonly string[] AllowedRoles = ["user", "assistant", "system"];

    private readonly GooseDbContext _db;

    public ConversationsController(GooseDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> List(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);

        var conversations = await _db.Conversations
            .OrderByDescending(x => x.UpdatedAt)
            .Take(take)
            .Select(x => new ConversationSummaryDto(
                x.Id,
                x.Title,
                x.CreatedAt,
                x.UpdatedAt,
                x.Messages.Count))
            .ToListAsync(cancellationToken);

        return Ok(conversations);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var conversation = await _db.Conversations
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return conversation is null ? NotFound() : Ok(ToDetail(conversation));
    }

    [HttpPost]
    public async Task<ActionResult<ConversationSummaryDto>> Create(
        CreateConversationRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(request.Title) ? "New conversation" : request.Title.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new { id = conversation.Id },
            new ConversationSummaryDto(conversation.Id, conversation.Title, now, now, 0));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ConversationSummaryDto>> Rename(
        Guid id,
        RenameConversationRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await _db.Conversations
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        conversation.Title = request.Title.Trim();
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToSummary(conversation));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        _db.Conversations.Remove(conversation);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<MessageDto>>> GetMessages(
        Guid id,
        CancellationToken cancellationToken)
    {
        var exists = await _db.Conversations.AnyAsync(x => x.Id == id, cancellationToken);
        if (!exists)
        {
            return NotFound();
        }

        var messages = await _db.Messages
            .Where(x => x.ConversationId == id)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MessageDto(x.Id, x.Role, x.Content, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<MessageDto>> AppendMessage(
        Guid id,
        AppendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (conversation is null)
        {
            return NotFound();
        }

        var role = request.Role.Trim().ToLowerInvariant();
        if (!AllowedRoles.Contains(role))
        {
            return ValidationProblem(
                $"Unsupported role '{request.Role}'. Use one of: {string.Join(", ", AllowedRoles)}.");
        }

        var now = DateTimeOffset.UtcNow;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = id,
            Role = role,
            Content = request.Content,
            CreatedAt = now,
        };

        _db.Messages.Add(message);
        conversation.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new MessageDto(message.Id, message.Role, message.Content, message.CreatedAt);
        return CreatedAtAction(nameof(GetMessages), new { id }, dto);
    }

    private static ConversationDetailDto ToDetail(Conversation conversation)
    {
        var messages = conversation.Messages
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MessageDto(x.Id, x.Role, x.Content, x.CreatedAt))
            .ToList();

        return new ConversationDetailDto(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            messages);
    }

    private static ConversationSummaryDto ToSummary(Conversation conversation)
    {
        return new ConversationSummaryDto(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            conversation.Messages.Count);
    }
}
