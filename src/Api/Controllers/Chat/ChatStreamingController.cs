using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Chatbot.Api.Controllers.Chat;

[ApiController]
[Authorize]
[Route("api/v1/chat")]
public sealed class ChatStreamingController : ChatControllerBase
{
    private readonly IChatOrchestrator _chatOrchestrator;
    private readonly ILogger<ChatStreamingController> _logger;

    public ChatStreamingController(IChatOrchestrator chatOrchestrator, ILogger<ChatStreamingController> logger)
    {
        _chatOrchestrator = chatOrchestrator;
        _logger = logger;
    }

    [HttpPost("stream")]
    [EnableRateLimiting("chat-stream")]
    public async Task<IActionResult> Stream([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
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
        catch (OperationCanceledException) when (WasRequestCancelled(cancellationToken))
        {
            _logger.LogInformation("Stream cancelled for session {sessionId}", request.SessionId);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Chat provider timed out during stream for session {sessionId}", request.SessionId);
            await WriteStreamErrorAsync("provider_timeout", "O provedor de IA excedeu o tempo limite da operacao.", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stream for session {sessionId}", request.SessionId);
            await WriteStreamErrorAsync("stream_error", "An error occurred during streaming", cancellationToken);
        }

        return new EmptyResult();
    }
}