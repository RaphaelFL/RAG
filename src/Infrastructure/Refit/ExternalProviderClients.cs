using Refit;

namespace Chatbot.Infrastructure.Refit;

public interface IAzureOpenAiClient
{
    [Get("/")]
    Task<ApiResponse<string>> GetRootAsync(CancellationToken cancellationToken = default);
}

public interface IAzureSearchClient
{
    [Get("/")]
    Task<ApiResponse<string>> GetRootAsync(CancellationToken cancellationToken = default);
}

public interface IBlobStorageClient
{
    [Get("/")]
    Task<ApiResponse<string>> GetRootAsync(CancellationToken cancellationToken = default);
}

public interface IGoogleVisionClient
{
    [Get("/")]
    Task<ApiResponse<string>> GetRootAsync(CancellationToken cancellationToken = default);
}