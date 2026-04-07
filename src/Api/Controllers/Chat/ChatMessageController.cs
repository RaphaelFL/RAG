using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers.Chat;

[ApiController]
[Authorize]
[Route("api/v1/chat")]
[Produces("application/json")]
public sealed class ChatMessageController : ChatControllerBase
{
    private readonly IChatOrchestrator _chatOrchestrator;
    private readonly ILogger<ChatMessageController> _logger;

    public ChatMessageController(IChatOrchestrator chatOrchestrator, ILogger<ChatMessageController> logger)
    {
        _chatOrchestrator = chatOrchestrator;
        _logger = logger;
    }

    [HttpPost("message")]
    [EnableRateLimiting("chat")]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponseDto>> SendMessage([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
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
        catch (OperationCanceledException ex) when (!WasRequestCancelled(cancellationToken))
        {
            _logger.LogWarning(ex, "Chat provider timed out for session {sessionId}", request.SessionId);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new ErrorResponseDto
            {
                Code = "provider_timeout",
                Message = "O provedor de IA excedeu o tempo limite da operacao.",
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
}