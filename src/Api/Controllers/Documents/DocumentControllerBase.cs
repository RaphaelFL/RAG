using Chatbot.Api.Documents;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Documents;

public abstract class DocumentControllerBase : ControllerBase
{
    protected Guid GetRequiredTenantId()
    {
        var rawTenantId = User.FindFirst("tenant_id")?.Value;
        if (!Guid.TryParse(rawTenantId, out var tenantId))
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return tenantId;
    }

    protected ObjectResult CreateValidationError(DocumentUploadValidationFailure validationFailure)
    {
        return StatusCode(validationFailure.StatusCode, new ErrorResponseDto
        {
            Code = validationFailure.Code,
            Message = validationFailure.Message,
            TraceId = HttpContext.TraceIdentifier
        });
    }

    protected ObjectResult CreateError(int statusCode, string code, string message)
    {
        return StatusCode(statusCode, new ErrorResponseDto
        {
            Code = code,
            Message = message,
            TraceId = HttpContext.TraceIdentifier
        });
    }
}