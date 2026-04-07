using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Unit.RetrievalServiceTestsSupport;

internal sealed class TestRequestContextAccessor : IRequestContextAccessor
{
    public Guid? TenantId { get; set; }
    public string? UserId { get; set; }
    public string? UserRole { get; set; }
}
