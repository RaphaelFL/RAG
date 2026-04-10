namespace Chatbot.Application.Abstractions;

public sealed class DuplicateDocumentException : Exception
{
    public DuplicateDocumentException(string message, Guid? existingDocumentId = null) : base(message)
    {
        ExistingDocumentId = existingDocumentId;
    }

    public Guid? ExistingDocumentId { get; }
}