using System.Data;
using Goose.Api.Contracts;
using Goose.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Goose.Api.Controllers;

/// <summary>Service and SQL Server reachability. The desktop status bar polls this.</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private const string DatabaseName = "GooseDb";

    private readonly GooseDbContext _db;
    private readonly ILogger<HealthController> _logger;

    public HealthController(GooseDbContext db, ILogger<HealthController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var connected = false;
        string? serverVersion = null;

        try
        {
            connected = await _db.Database.CanConnectAsync(cancellationToken);
            if (connected)
            {
                serverVersion = await ReadServerVersionAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GooseDb health check failed");
        }

        var response = new HealthResponse(
            connected ? "ok" : "degraded",
            "GooseApi",
            DatabaseName,
            connected,
            serverVersion,
            DateTimeOffset.UtcNow);

        // 503 tells clients to back off; the body still describes the failure.
        return connected ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    private async Task<string?> ReadServerVersionAsync(CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT @@VERSION";
        var raw = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // "Microsoft SQL Server 2022 ..." is enough; drop the copyright lines.
        var firstLine = raw.Split('\n')[0].Trim();
        return firstLine.Length > 80 ? firstLine[..80] : firstLine;
    }
}
