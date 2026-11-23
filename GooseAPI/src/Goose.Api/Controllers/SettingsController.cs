using Goose.Api.Contracts;
using Goose.Api.Data;
using Goose.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Goose.Api.Controllers;

/// <summary>Key/value settings the desktop clients sync.</summary>
[ApiController]
[Route("api/settings")]
public sealed class SettingsController : ControllerBase
{
    private readonly GooseDbContext _db;

    public SettingsController(GooseDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SettingDto>>> List(CancellationToken cancellationToken)
    {
        var settings = await _db.Settings
            .OrderBy(x => x.Key)
            .Select(x => new SettingDto(x.Key, x.Value, x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(settings);
    }

    [HttpGet("{key:maxlength(200)}")]
    public async Task<ActionResult<SettingDto>> Get(string key, CancellationToken cancellationToken)
    {
        var setting = await _db.Settings
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        return setting is null
            ? NotFound()
            : Ok(new SettingDto(setting.Key, setting.Value, setting.UpdatedAt));
    }

    [HttpPut("{key:maxlength(200)}")]
    public async Task<ActionResult<SettingDto>> Upsert(
        string key,
        UpsertSettingRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var setting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        if (setting is null)
        {
            setting = new Setting { Key = key, Value = request.Value, UpdatedAt = now };
            _db.Settings.Add(setting);
        }
        else
        {
            setting.Value = request.Value;
            setting.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new SettingDto(setting.Key, setting.Value, setting.UpdatedAt));
    }

    [HttpDelete("{key:maxlength(200)}")]
    public async Task<IActionResult> Delete(string key, CancellationToken cancellationToken)
    {
        var setting = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
        if (setting is null)
        {
            return NotFound();
        }

        _db.Settings.Remove(setting);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
