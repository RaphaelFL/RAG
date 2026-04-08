using AppCfg = Chatbot.Application.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Chatbot.Infrastructure.Persistence;

internal sealed class RedisIndexEnsurer : IRedisIndexEnsurer
{
    private readonly AppCfg.VectorStoreOptions _vectorOptions;
    private bool _indexEnsured;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public RedisIndexEnsurer(IOptions<AppCfg.VectorStoreOptions> vectorOptions)
    {
        _vectorOptions = vectorOptions.Value;
    }

    public async Task EnsureIndexAsync(IDatabase database, CancellationToken ct)
    {
        if (_indexEnsured)
        {
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_indexEnsured)
            {
                return;
            }

            try
            {
                await database.ExecuteAsync("FT.INFO", _vectorOptions.IndexName);
                _indexEnsured = true;
                return;
            }
            catch
            {
            }

            var createArguments = new List<object>
            {
                _vectorOptions.IndexName,
                "ON", "HASH",
                "PREFIX", 1, _vectorOptions.KeyPrefix,
                "SCHEMA",
                "chunkId", "TAG",
                "tenantId", "TAG",
                "documentId", "TAG",
                "chunkIndex", "NUMERIC",
                "content", "TEXT",
                "title", "TEXT",
                "sourceName", "TEXT",
                "sourceType", "TAG",
                "contentType", "TAG",
                "sectionTitle", "TEXT",
                "pageNumber", "NUMERIC",
                "endPageNumber", "NUMERIC",
                "tags", "TAG", "SEPARATOR", "|",
                "categories", "TAG", "SEPARATOR", "|",
                "accessPolicy", "TAG",
                "contentHash", "TAG",
                "metadata", "TEXT",
                "vector", "VECTOR", "HNSW", 6,
                "TYPE", "FLOAT32",
                "DIM", _vectorOptions.Dimensions,
                "DISTANCE_METRIC", "COSINE"
            };

            await database.ExecuteAsync("FT.CREATE", createArguments.ToArray());
            _indexEnsured = true;
        }
        finally
        {
            _lock.Release();
        }
    }
}