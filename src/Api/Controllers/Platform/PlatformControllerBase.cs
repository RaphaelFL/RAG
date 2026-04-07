using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Chatbot.Api.Controllers.Platform;

public abstract class PlatformControllerBase : ControllerBase
{
    protected Guid GetTenantId()
    {
        var tenantIdRaw = User.FindFirst("tenant_id")?.Value;
        return Guid.TryParse(tenantIdRaw, out var tenantId)
            ? tenantId
            : Guid.Empty;
    }

    protected static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}