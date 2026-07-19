namespace LocalRagLab.Api.Options;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public int ChunkSizeCharacters { get; init; } = 1200;
    public int ChunkOverlapCharacters { get; init; } = 180;
    public int CandidateCount { get; init; } = 12;
    public int ContextCount { get; init; } = 4;
    public double MinimumSimilarity { get; init; } = 0.2;
    public double SemanticWeight { get; init; } = 0.85;
    public double LexicalWeight { get; init; } = 0.15;
    public long MaxUploadBytes { get; init; } = 10 * 1024 * 1024;
    public int TraceCapacity { get; init; } = 100;
}
