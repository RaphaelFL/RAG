using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Chat;

public abstract class ChatControllerBase : ControllerBase
{
    protected static readonly JsonSerializerOptions StreamingJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected bool WasRequestCancelled(CancellationToken cancellationToken)
    {
        return cancellationToken.IsCancellationRequested || HttpContext.RequestAborted.IsCancellationRequested;
    }

    protected async Task WriteStreamErrorAsync(string code, string message, CancellationToken cancellationToken)
    {
        if (WasRequestCancelled(cancellationToken))
        {
            return;
        }

        var errorEvent = JsonSerializer.Serialize(new StreamErrorEventDto
        {
            Code = code,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        }, StreamingJsonOptions);

        if (!Response.HasStarted)
        {
            Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
        }

        await Response.WriteAsync("event: error\r\n");
        await Response.WriteAsync($"data: {errorEvent}\r\n\r\n");
        await Response.Body.FlushAsync(cancellationToken);
    }

    protected Guid? TryGetTenantId()
    {
        var tenantIdRaw = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantIdRaw, out var tenantId)
            ? tenantId
            : null;
    }
}