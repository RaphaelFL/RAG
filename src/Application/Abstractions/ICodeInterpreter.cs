namespace Chatbot.Application.Abstractions;

public interface ICodeInterpreter
{
    Task<CodeInterpreterResult> ExecuteAsync(CodeInterpreterRequest request, CancellationToken ct);
}