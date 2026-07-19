namespace LocalRagLab.Api.Options;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; init; } = "http://localhost:11434";
    public string ChatModel { get; init; } = "llama3.2:3b";
    public string EmbeddingModel { get; init; } = "nomic-embed-text-v2-moe:latest";
    public bool UseNomicSearchPrefixes { get; init; } = true;
    public int RequestTimeoutSeconds { get; init; } = 300;
    public string KeepAlive { get; init; } = "10m";
    public double Temperature { get; init; } = 0.1;
}
