using System.Threading;
using Chatbot.Application.Abstractions;

namespace Chatbot.Api.Authentication;

public sealed class RequestContextAccessor : IRequestContextAccessor
{
    private static readonly AsyncLocal<Guid?> Tenant = new();
    private static readonly AsyncLocal<string?> User = new();
    private static readonly AsyncLocal<string?> Role = new();

    public Guid? TenantId
    {
        get => Tenant.Value;
        set => Tenant.Value = value;
    }

    public string? UserId
    {
        get => User.Value;
        set => User.Value = value;
    }

    public string? UserRole
    {
        get => Role.Value;
        set => Role.Value = value;
    }
}
