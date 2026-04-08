namespace Chatbot.Application.Abstractions;

public interface IPromptContentBuilder
{
    PromptAssemblyResult Build(PromptAssemblyRequest request, IReadOnlyList<RetrievedChunk> selectedChunks);
}