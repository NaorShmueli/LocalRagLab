namespace LocalRagLab.Api.Domain;

public sealed record RagTraceCandidate(
    string ChunkId,
    string DocumentId,
    string Title,
    int ChunkIndex,
    int? PageNumber,
    double SimilarityScore,
    double LexicalScore,
    double FinalScore,
    bool Selected,
    string TextPreview);

public sealed record RagTraceRecord(
    string TraceId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string TenantId,
    string UserId,
    IReadOnlyCollection<string> Roles,
    string Question,
    string EmbeddingModel,
    int QueryEmbeddingDimensions,
    IReadOnlyList<float> QueryEmbeddingPreview,
    IReadOnlyList<RagTraceCandidate> Candidates,
    PromptPackage? Prompt,
    string? ChatModel,
    ModelUsage? ChatUsage,
    string Answer,
    bool UsedFallback,
    GroundednessResult? Groundedness,
    IReadOnlyDictionary<string, double> StageMilliseconds,
    string? Failure);
