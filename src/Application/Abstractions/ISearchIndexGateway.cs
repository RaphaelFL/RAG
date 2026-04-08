namespace Chatbot.Application.Abstractions;

public interface ISearchIndexGateway : IDocumentChunkIndexer, IDocumentChunkReader, IHybridSearchGateway, IDocumentIndexDeleter
{
}