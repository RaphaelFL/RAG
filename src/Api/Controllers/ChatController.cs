using Chatbot.Application.Contracts;
using Chatbot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;

namespace Chatbot.Api.Controllers;

/// <summary>
/// Chat endpoint para interação com o chatbot grounded
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/chat")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private static readonly JsonSerializerOptions StreamingJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IChatOrchestrator _chatOrchestrator;
    private readonly IChatSessionStore _chatSessionStore;
    private readonly IDocumentCatalog _documentCatalog;
    private readonly IDocumentAuthorizationService _documentAuthorizationService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatOrchestrator chatOrchestrator,
        IChatSessionStore chatSessionStore,
        IDocumentCatalog documentCatalog,
        IDocumentAuthorizationService documentAuthorizationService,
        ILogger<ChatController> logger)
    {
        _chatOrchestrator = chatOrchestrator;
        _chatSessionStore = chatSessionStore;
        _documentCatalog = documentCatalog;
        _documentAuthorizationService = documentAuthorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Enviar pergunta e obter resposta não-streaming
    /// </summary>
    [HttpPost("message")]
    [EnableRateLimiting("chat")]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponseDto>> SendMessage(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chat message received for session {sessionId}", request.SessionId);

        try
        {
            var response = await _chatOrchestrator.SendAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation");
            return BadRequest(new ErrorResponseDto
            {
                Code = "invalid_operation",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access");
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponseDto
            {
                Code = "access_denied",
                Message = ex.Message,
                TraceId = HttpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SendMessage");
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseDto
            {
                Code = "internal_error",
                Message = "An unexpected error occurred",
                TraceId = HttpContext.TraceIdentifier
            });
        }
    }

    /// <summary>
    /// Enviar pergunta com resposta em streaming (SSE)
    /// </summary>
    [HttpPost("stream")]
    [EnableRateLimiting("chat-stream")]
    [Produces("text/event-stream")]
    public async Task Stream(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chat stream started for session {sessionId}", request.SessionId);

        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            await foreach (var @event in _chatOrchestrator.StreamAsync(request, cancellationToken))
            {
                var json = JsonSerializer.Serialize(@event, StreamingJsonOptions);
                await Response.WriteAsync($"event: {@event.EventType}\r\n");
                await Response.WriteAsync($"data: {json}\r\n\r\n");
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stream cancelled for session {sessionId}", request.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stream for session {sessionId}", request.SessionId);
            var errorEvent = JsonSerializer.Serialize(new StreamErrorEventDto
            {
                Code = "stream_error",
                Message = "An error occurred during streaming",
                TraceId = HttpContext.TraceIdentifier
            }, StreamingJsonOptions);
            await Response.WriteAsync($"event: error\r\n");
            await Response.WriteAsync($"data: {errorEvent}\r\n\r\n");
        }
    }

    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(ChatSessionSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public ActionResult<ChatSessionSnapshot> GetSession([FromRoute] Guid sessionId)
    {
        var tenantIdRaw = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            return Unauthorized(new ErrorResponseDto
            {
                Code = "unauthorized",
                Message = "Authentication is required",
                TraceId = HttpContext.TraceIdentifier
            });
        }

        var session = _chatSessionStore.Get(sessionId, tenantId);
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
        var filteredMessages = session.Messages.Select(message =>
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
        }).ToList();

        session.Messages = filteredMessages;

        return Ok(session);
    }
}
