using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Chatbot.Application.Abstractions;
using Chatbot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Chatbot.Infrastructure.Persistence;

public sealed class AzureBlobStorageGateway : IBlobStorageGateway
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageGateway(IOptions<BlobStorageOptions> options)
    {
        var blobOptions = options.Value;
        var serviceClient = new BlobServiceClient(blobOptions.ConnectionString);
        _containerClient = serviceClient.GetBlobContainerClient(blobOptions.ContainerName);
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> SaveAsync(Stream content, string path, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        var blobClient = _containerClient.GetBlobClient(path);
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken: ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return path;
    }

    public async Task<Stream> GetAsync(string path, CancellationToken ct)
    {
        var blobClient = _containerClient.GetBlobClient(path);
        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        var buffer = new MemoryStream();
        await response.Value.Content.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string path, CancellationToken ct)
    {
        var blobClient = _containerClient.GetBlobClient(path);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }
}