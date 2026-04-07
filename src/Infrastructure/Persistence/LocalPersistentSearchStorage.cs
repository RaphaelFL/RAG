using System.Text.Json;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class LocalPersistentSearchStorage
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly object _sync = new();
    private readonly string _indexFilePath;
    private Dictionary<string, LocalPersistentIndexedChunk> _index;

    public LocalPersistentSearchStorage(IOptions<LocalPersistenceOptions> options, IHostEnvironment environment)
    {
        var basePath = ResolveBasePath(options.Value.BasePath, environment.ContentRootPath);
        Directory.CreateDirectory(basePath);
        _indexFilePath = Path.Combine(basePath, options.Value.SearchIndexFileName);
        _index = Load();
    }

    public void Upsert(List<DocumentChunkIndexDto> chunks)
    {
        lock (_sync)
        {
            foreach (var chunk in chunks)
            {
                _index[chunk.ChunkId] = LocalPersistentIndexedChunk.From(chunk);
            }

            Persist();
        }
    }

    public List<DocumentChunkIndexDto> GetDocumentChunks(Guid documentId)
    {
        lock (_sync)
        {
            return _index.Values
                .Where(item => item.DocumentId == documentId)
                .OrderBy(item => item.GetChunkIndex())
                .ThenBy(item => item.ChunkId, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.ToDocumentChunk())
                .ToList();
        }
    }

    public List<LocalPersistentIndexedChunk> GetAll()
    {
        lock (_sync)
        {
            return _index.Values.Select(LocalPersistentIndexedChunk.Clone).ToList();
        }
    }

    public void DeleteDocument(Guid documentId)
    {
        lock (_sync)
        {
            var idsToRemove = _index.Values
                .Where(item => item.DocumentId == documentId)
                .Select(item => item.ChunkId)
                .ToList();

            foreach (var chunkId in idsToRemove)
            {
                _index.Remove(chunkId);
            }

            Persist();
        }
    }

    private Dictionary<string, LocalPersistentIndexedChunk> Load()
    {
        if (!File.Exists(_indexFilePath))
        {
            return new Dictionary<string, LocalPersistentIndexedChunk>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(_indexFilePath);
            var values = JsonSerializer.Deserialize<List<LocalPersistentIndexedChunk>>(json, SerializerOptions) ?? new List<LocalPersistentIndexedChunk>();
            return values.ToDictionary(item => item.ChunkId, LocalPersistentIndexedChunk.Clone, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, LocalPersistentIndexedChunk>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void Persist()
    {
        var tempFilePath = _indexFilePath + ".tmp";
        var payload = JsonSerializer.Serialize(_index.Values.OrderBy(item => item.DocumentId).ThenBy(item => item.ChunkId).ToList(), SerializerOptions);
        File.WriteAllText(tempFilePath, payload);
        File.Move(tempFilePath, _indexFilePath, overwrite: true);
    }

    private static string ResolveBasePath(string configuredPath, string contentRootPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));
    }
}