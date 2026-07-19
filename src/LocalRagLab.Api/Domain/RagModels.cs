namespace LocalRagLab.Api.Domain;

public sealed record VectorCandidate(
    StoredChunk Chunk,
    double SimilarityScore);

public sealed record RerankedCandidate(
    StoredChunk Chunk,
    double SimilarityScore,
    double LexicalScore,
    double FinalScore,
    bool IsSelected);

public sealed record PromptSource(
    string Label,
    string ChunkId,
    string DocumentId,
    string Title,
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    string Text);

public sealed record PromptPackage(
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyList<PromptSource> Sources);

public sealed record ModelUsage(
    long? TotalDurationNanoseconds,
    long? LoadDurationNanoseconds,
    int? PromptEvaluationCount,
    int? EvaluationCount,
    long? PromptEvaluationDurationNanoseconds,
    long? EvaluationDurationNanoseconds);

public sealed record ChatCompletionResult(
    string Content,
    string Model,
    ModelUsage Usage);

public sealed record EmbeddingResult(
    float[] Vector,
    string Model,
    ModelUsage Usage);

public sealed record GroundednessResult(
    bool EvaluationCompleted,
    bool? IsGrounded,
    double? Score,
    string Explanation,
    IReadOnlyList<string> UnsupportedClaims,
    string? RawModelOutput);
