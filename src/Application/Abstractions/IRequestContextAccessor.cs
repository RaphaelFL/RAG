namespace Chatbot.Application.Abstractions;

public interface IRequestContextAccessor
{
    Guid? TenantId { get; set; }
    string? UserId { get; set; }
    string? UserRole { get; set; }
}