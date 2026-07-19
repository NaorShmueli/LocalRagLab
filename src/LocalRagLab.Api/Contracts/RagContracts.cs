using System.ComponentModel.DataAnnotations;
using LocalRagLab.Api.Domain;

namespace LocalRagLab.Api.Contracts;

public sealed class AskRagRequest
{
    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    public string UserId { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    [Required]
    public string Question { get; init; } = string.Empty;

    public bool EvaluateGroundedness { get; init; }
}

public sealed record RagCitationResponse(
    string Label,
    string ChunkId,
    string DocumentId,
    string Title,
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    double RetrievalScore);

public sealed record RagDebugResponse(
    string EmbeddingModel,
    int QueryEmbeddingDimensions,
    IReadOnlyList<float> QueryEmbeddingPreview,
    IReadOnlyList<RagTraceCandidate> Candidates,
    PromptPackage? Prompt,
    string? ChatModel,
    ModelUsage? ChatUsage,
    IReadOnlyDictionary<string, double> StageMilliseconds);

public sealed record AskRagResponse(
    string TraceId,
    string Answer,
    bool UsedFallback,
    IReadOnlyList<RagCitationResponse> Citations,
    GroundednessResult? Groundedness,
    RagDebugResponse Debug);
