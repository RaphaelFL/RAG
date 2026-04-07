namespace Chatbot.Application.Abstractions;

public interface IChatStreamingSegmenter
{
    IEnumerable<string> Segment(string text);
}