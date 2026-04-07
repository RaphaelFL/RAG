namespace Chatbot.Application.Abstractions;

public delegate ValueTask BackgroundWorkItem(IServiceProvider serviceProvider, CancellationToken ct);