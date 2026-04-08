namespace Chatbot.Infrastructure.Persistence;

internal sealed record LocalCacheEntry(string Payload, DateTime ExpiresAtUtc);