using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Contracts;
using Chatbot.Application.Services;
using Chatbot.Infrastructure.Configuration;
using Chatbot.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Xunit;

namespace Backend.Unit.RagPersistenceTestsSupport;

internal sealed class NoOpBlobStorageGateway : IBlobStorageGateway
{
    public Task DeleteAsync(string path, CancellationToken ct) => Task.CompletedTask;

    public Task<Stream> GetAsync(string path, CancellationToken ct)
    {
        return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("conteudo")));
    }

    public Task<string> SaveAsync(Stream content, string path, CancellationToken ct) => Task.FromResult(path);
}
