using System.ComponentModel.DataAnnotations;
using LocalRagLab.Api.Domain;

namespace LocalRagLab.Api.Contracts;

public sealed class IngestTextRequest
{
    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    public string DocumentId { get; init; } = string.Empty;

    [Required]
    public string Title { get; init; } = string.Empty;

    public string? RequiredRole { get; init; }

    [Required]
    public string Text { get; init; } = string.Empty;
}

public sealed class UploadDocumentRequest
{
    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    public string DocumentId { get; init; } = string.Empty;

    [Required]
    public string Title { get; init; } = string.Empty;

    public string? RequiredRole { get; init; }

    [Required]
    public IFormFile File { get; init; } = default!;
}

public sealed record IngestDocumentResponse(
    DocumentInfo Document,
    IReadOnlyList<ChunkSummaryResponse> Chunks);

public sealed record ChunkSummaryResponse(
    string ChunkId,
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    int CharacterCount,
    int EmbeddingDimensions,
    IReadOnlyList<float> EmbeddingPreview,
    string Text);

public sealed record DocumentChunkResponse(
    string ChunkId,
    string TenantId,
    string DocumentId,
    string Title,
    string? RequiredRole,
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    string Text,
    int EmbeddingDimensions,
    IReadOnlyList<float>? Embedding);
