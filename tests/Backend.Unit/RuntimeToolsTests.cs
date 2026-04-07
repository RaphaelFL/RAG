using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Chatbot.Application.Abstractions;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Tools;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

using Backend.Unit.RuntimeToolsTestsSupport;

namespace Backend.Unit;

public class RuntimeToolsTests
{
    [Fact]
    public async Task GuardedWebSearchTool_ShouldParseAllowedResults_AndUseCache()
    {
        var tenantId = Guid.NewGuid();
        var cache = new InMemoryTestCache();
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri is not null && request.RequestUri.Host.Contains("duckduckgo", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        <html><body>
                          <a class="result__a" href="https://docs.contoso.com/rag/guide">RAG Guide</a>
                          <div class="result__snippet">Documentacao principal da plataforma RAG.</div>
                        </body></html>
                        """, Encoding.UTF8, "text/html")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var factory = new StubHttpClientFactory(handler);
        var tool = new GuardedWebSearchTool(
            factory,
            cache,
            Options.Create(new WebSearchOptions
            {
                Enabled = true,
                DefaultTopK = 5,
                TimeoutSeconds = 5,
                AllowedHosts = ["docs.contoso.com"]
            }),
            NullLogger<GuardedWebSearchTool>.Instance);

        var first = await tool.SearchAsync(new WebSearchRequest
        {
            TenantId = tenantId,
            Query = "rag plataforma",
            TopK = 3
        }, CancellationToken.None);

        var second = await tool.SearchAsync(new WebSearchRequest
        {
            TenantId = tenantId,
            Query = "rag plataforma",
            TopK = 3
        }, CancellationToken.None);

        first.Hits.Should().ContainSingle();
        first.Hits.First().Title.Should().Be("RAG Guide");
        first.Hits.First().Url.Should().Be("https://docs.contoso.com/rag/guide");
        first.Hits.First().Snippet.Should().Contain("plataforma RAG");
        second.Hits.Should().ContainSingle();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task GuardedPythonCodeInterpreter_ShouldRejectDangerousCode()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"rag-code-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            var interpreter = new GuardedPythonCodeInterpreter(
                Options.Create(new CodeInterpreterOptions
                {
                    Enabled = true,
                    Runtime = "python",
                    TimeoutSeconds = 5,
                    MemoryLimitMb = 256,
                    WorkingDirectoryRoot = "artifacts/code-interpreter-test"
                }),
                new TestHostEnvironment { ContentRootPath = rootPath },
                NullLogger<GuardedPythonCodeInterpreter>.Instance);

            var result = await interpreter.ExecuteAsync(new CodeInterpreterRequest
            {
                TenantId = Guid.NewGuid(),
                Language = "python",
                Code = "import subprocess\nprint('nao deve rodar')"
            }, CancellationToken.None);

            result.ExitCode.Should().Be(-1);
            result.StdErr.Should().Contain("policy de seguranca");
            result.OutputArtifacts.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }




}
