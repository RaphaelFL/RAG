namespace Chatbot.Infrastructure.Persistence;

internal sealed class BlobContentBuffer
{
    public async Task<byte[]> ReadAsync(Stream content, CancellationToken ct)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        return buffer.ToArray();
    }
}