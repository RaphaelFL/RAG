namespace Chatbot.Application.Abstractions;

public interface IBlobStorageGateway : IBlobContentWriter, IBlobContentReader, IBlobContentDeleter
{
}