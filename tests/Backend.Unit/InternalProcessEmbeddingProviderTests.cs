using System.Reflection;
using Chatbot.Application.Configuration;
using Chatbot.Infrastructure.Embeddings;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Backend.Unit;

public class InternalProcessEmbeddingProviderTests
{
    [Fact]
    public void ResolvePath_ShouldProbeParentDirectories_ForRepositoryRelativePaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(tempRoot, "src", "Api");
        var toolsDirectory = Path.Combine(tempRoot, "tools", "embeddings");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(toolsDirectory);

        var expectedPath = Path.Combine(toolsDirectory, "embed_runtime.py");
        File.WriteAllText(expectedPath, "# runtime");

        try
        {
            var provider = new InternalProcessEmbeddingProvider(
                Options.Create(new EmbeddingGenerationOptions
                {
                    RuntimeScriptPath = "tools/embeddings/embed_runtime.py",
                    MaxConcurrency = 1
                }),
                new TestHostEnvironment { ContentRootPath = contentRoot },
                NullLogger<InternalProcessEmbeddingProvider>.Instance);

            var resolvedPath = InvokeResolvePath(provider, "tools/embeddings/embed_runtime.py");

            resolvedPath.Should().Be(expectedPath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static string InvokeResolvePath(InternalProcessEmbeddingProvider provider, string path)
    {
        var method = typeof(InternalProcessEmbeddingProvider).GetMethod("ResolvePath", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (string)method!.Invoke(provider, new object[] { path })!;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Backend.Unit";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}