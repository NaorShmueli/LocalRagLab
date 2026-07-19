using System.ComponentModel.DataAnnotations;

namespace LocalRagLab.Api.Contracts;

public sealed class SemanticSearchRequest
{
    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    public string UserId { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Roles { get; init; } = Array.Empty<string>();

    [Required]
    public string Query { get; init; } = string.Empty;

    [Range(1, 50)]
    public int? TopK { get; init; }

    [Range(-1, 1)]
    public double? MinimumSimilarity { get; init; }
}

public sealed record SemanticSearchResponse(
    string Query,
    string EmbeddingModel,
    int EmbeddingDimensions,
    IReadOnlyList<float> EmbeddingPreview,
    IReadOnlyList<SearchResultResponse> Results,
    double EmbeddingMilliseconds,
    double SearchMilliseconds);

public sealed record SearchResultResponse(
    int Rank,
    string ChunkId,
    string DocumentId,
    string Title,
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    double SimilarityScore,
    string Text);
