using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Chatbot.Application.Observability;

public static class ChatbotTelemetry
{
    public const string ActivitySourceName = "Chatbot.Application";
    public const string MeterName = "Chatbot.Application";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> IngestionJobsQueued = Meter.CreateCounter<long>("chatbot.ingestion.jobs.queued");
    public static readonly Counter<long> ReindexJobsQueued = Meter.CreateCounter<long>("chatbot.reindex.jobs.queued");
    public static readonly Counter<long> PromptInjectionSignals = Meter.CreateCounter<long>("chatbot.security.prompt_injection.signals");
    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("chatbot.cache.hits");
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("chatbot.cache.misses");
    public static readonly Counter<long> OcrAvoided = Meter.CreateCounter<long>("chatbot.ocr.avoided");
    public static readonly Counter<long> EmbeddingReuse = Meter.CreateCounter<long>("chatbot.embedding.reuse");
    public static readonly Counter<long> EmbeddingFallbacks = Meter.CreateCounter<long>("chatbot.embedding.fallbacks");
    public static readonly Counter<long> AgentToolInvocations = Meter.CreateCounter<long>("chatbot.agent.tool.invocations");
    public static readonly Histogram<double> RetrievalLatencyMs = Meter.CreateHistogram<double>("chatbot.retrieval.latency.ms");
    public static readonly Histogram<double> IngestionLatencyMs = Meter.CreateHistogram<double>("chatbot.ingestion.latency.ms");
    public static readonly Histogram<double> EmbeddingLatencyMs = Meter.CreateHistogram<double>("chatbot.embedding.latency.ms");
    public static readonly Histogram<double> VectorStoreLatencyMs = Meter.CreateHistogram<double>("chatbot.vector_store.latency.ms");
    public static readonly Histogram<double> PromptAssemblyLatencyMs = Meter.CreateHistogram<double>("chatbot.prompt_assembly.latency.ms");
}