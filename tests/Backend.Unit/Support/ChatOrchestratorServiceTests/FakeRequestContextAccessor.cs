using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit.ChatOrchestratorServiceTestsSupport;

internal sealed class FakeRequestContextAccessor : IRequestContextAccessor
{
    public Guid? TenantId { get; set; } = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    public string? UserId { get; set; } = "bbbbbbbb-2222-2222-2222-222222222222";
    public string? UserRole { get; set; } = "TenantAdmin";
}
