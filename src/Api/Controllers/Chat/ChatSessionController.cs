using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Chat;

[ApiController]
[Authorize]
[Route("api/v1/chat")]
[Produces("application/json")]
public sealed class ChatSessionController : ChatControllerBase
{
    private readonly IChatSessionStore _chatSessionStore;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;

    public ChatSessionController(
        IChatSessionStore chatSessionStore,
        IDocumentCatalog documentCatalog,
        IDocumentAuthorizationService documentAuthorizationService)
    {
        _chatSessionStore = chatSessionStore;
        _documentCatalog = documentCatalog;
        _documentAuthorizationService = documentAuthorizationService;
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(ChatSessionSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatSessionSnapshot>> GetSession([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var tenantId = TryGetTenantId();
        if (tenantId is null)
        {
            return Unauthorized(new ErrorResponseDto
            {
                Code = "unauthorized",
                Message = "Authentication is required",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var session = await _chatSessionStore.GetAsync(sessionId, tenantId.Value, cancellationToken);
        if (session is null)
        {
            return NotFound(new ErrorResponseDto
            {
                Code = "session_not_found",
                Message = $"Session {sessionId} not found",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        session.Messages = session.Messages.Select(message => FilterMessage(message, tenantId.Value, userId, userRole)).ToList();

        return Ok(session);
    }

    private ChatSessionMessageSnapshot FilterMessage(ChatSessionMessageSnapshot message, Guid tenantId, string? userId, string? userRole)
    {
        if (!string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) || message.Citations.Count == 0)
        {
            return message;
        }

        var visibleCitations = message.Citations
            .Where(citation =>
            {
                var document = _documentCatalog.Get(citation.DocumentId);
                return document is not null && _documentAuthorizationService.CanAccess(document, tenantId, userId, userRole);
            })
            .ToList();

        if (visibleCitations.Count == message.Citations.Count)
        {
            return message;
        }

        return new ChatSessionMessageSnapshot
        {
            MessageId = message.MessageId,
            Role = message.Role,
            Content = "Conteudo ocultado por autorizacao documental atual.",
            Citations = visibleCitations,
            Usage = visibleCitations.Count > 0 ? message.Usage : null,
            CreatedAtUtc = message.CreatedAtUtc,
            TemplateVersion = message.TemplateVersion
        };
    }
}