namespace Chatbot.Application.Abstractions;

public sealed class DuplicateDocumentException : Exception
{
    public DuplicateDocumentException(string message) : base(message)
    {
    }
}