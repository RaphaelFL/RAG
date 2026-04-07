using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

[ApiController]
[Authorize]
[Route("api/v1/platform")]
[Produces("application/json")]
public sealed class PlatformOperationalAuditController : PlatformControllerBase
{
    private readonly IOperationalAuditReader _operationalAuditReader;

    public PlatformOperationalAuditController(IOperationalAuditReader operationalAuditReader)
    {
        _operationalAuditReader = operationalAuditReader;
    }

    [HttpGet("operational-audit")]
    [Authorize(Policy = "DocumentAdmin")]
    public async Task<ActionResult<OperationalAuditFeedResponseDto>> GetOperationalAudit(
        [FromQuery] string? category,
        [FromQuery] string? status,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _operationalAuditReader.ReadAuditFeedAsync(new OperationalAuditFeedQuery
        {
            TenantId = GetTenantId(),
            Category = category,
            Status = status,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Cursor = cursor,
            Limit = limit
        }, cancellationToken);

        return Ok(new OperationalAuditFeedResponseDto
        {
            Entries = result.Entries.Select(entry => new OperationalAuditEntryDto
            {
                EntryId = entry.EntryId,
                Category = entry.Category,
                Status = entry.Status,
                Title = entry.Title,
                Summary = entry.Summary,
                DetailsJson = entry.DetailsJson,
                CreatedAtUtc = entry.CreatedAtUtc,
                CompletedAtUtc = entry.CompletedAtUtc
            }).ToList(),
            NextCursor = result.NextCursor
        });
    }
}