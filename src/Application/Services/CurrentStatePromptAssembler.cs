namespace Chatbot.Application.Services;

public sealed class CurrentStatePromptAssembler : IPromptAssembler
{
    private readonly IPromptChunkSelector _promptChunkSelector;
    private readonly IPromptContentBuilder _promptContentBuilder;
    private readonly IPromptAssemblyAuditLogger _promptAssemblyAuditLogger;

    public CurrentStatePromptAssembler(
        IPromptChunkSelector promptChunkSelector,
        IPromptContentBuilder promptContentBuilder,
        IPromptAssemblyAuditLogger promptAssemblyAuditLogger)
    {
        _promptChunkSelector = promptChunkSelector;
        _promptContentBuilder = promptContentBuilder;
        _promptAssemblyAuditLogger = promptAssemblyAuditLogger;
    }

    public CurrentStatePromptAssembler(IOperationalAuditWriter operationalAuditWriter)
        : this(
            new PromptChunkSelector(),
            new PromptContentBuilder(),
            new PromptAssemblyAuditLogger(operationalAuditWriter))
    {
    }

    public async Task<PromptAssemblyResult> AssembleAsync(PromptAssemblyRequest request, CancellationToken ct)
    {
        var selectedChunks = _promptChunkSelector.Select(request);
        var result = _promptContentBuilder.Build(request, selectedChunks);

        await _promptAssemblyAuditLogger.WriteAsync(request, result, ct);

        return result;
    }
}
