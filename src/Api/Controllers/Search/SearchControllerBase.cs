using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Search;

public abstract class SearchControllerBase : ControllerBase
{
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