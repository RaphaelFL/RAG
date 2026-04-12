using Chatbot.Application.Abstractions;

namespace Chatbot.Ingestion.Chunking;

public sealed class SlidingWindowChunkingStrategy : IChunkingStrategy
{
    private readonly SlidingWindowWindowResolver _windowResolver;
    private readonly SlidingWindowParagraphBuilder _paragraphBuilder;
    private readonly SlidingWindowChunkFactory _chunkFactory;

    public SlidingWindowChunkingStrategy(IRagRuntimeSettings runtimeSettings)
        : this(
            new SlidingWindowWindowResolver(runtimeSettings),
            new SlidingWindowParagraphBuilder(),
            new SlidingWindowChunkFactory())
    {
    }

    internal SlidingWindowChunkingStrategy(
        SlidingWindowWindowResolver windowResolver,
        SlidingWindowParagraphBuilder paragraphBuilder,
        SlidingWindowChunkFactory chunkFactory)
    {
        _windowResolver = windowResolver;
        _paragraphBuilder = paragraphBuilder;
        _chunkFactory = chunkFactory;
    }

    public List<DocumentChunkIndexDto> Chunk(IngestDocumentCommand command, DocumentTextExtractionResultDto extraction)
    {
        var sourceText = _paragraphBuilder.NormalizeSourceText(command, extraction.Text);
        var pageCount = extraction.Pages.Count > 0 ? extraction.Pages.Count : 1;
        var maxTokens = _windowResolver.ResolveMaxTokens(sourceText, command.ContentLength, pageCount);
        var overlapTokens = _windowResolver.ResolveOverlapTokens(sourceText, command.ContentLength, pageCount);
        var paragraphs = _paragraphBuilder.Build(command, extraction, maxTokens);
        return _chunkFactory.Build(command, sourceText, paragraphs, maxTokens, overlapTokens);
    }
}
