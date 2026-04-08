namespace Chatbot.Application.Abstractions;

public interface IPromptChunkSelector
{
    IReadOnlyList<RetrievedChunk> Select(PromptAssemblyRequest request);
}