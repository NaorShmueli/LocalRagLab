namespace LocalRagLab.Api.Domain;

public sealed record SourceSegment(
    int? PageNumber,
    string? SectionTitle,
    string Text);

public sealed record ExtractedDocument(
    string FileName,
    string MediaType,
    IReadOnlyList<SourceSegment> Segments);

public sealed record ChunkDraft(
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    string Text);

public sealed record DocumentInfo(
    string TenantId,
    string DocumentId,
    string Title,
    string? RequiredRole,
    string SourceType,
    string? FileName,
    DateTimeOffset CreatedAt,
    int ChunkCount,
    int EmbeddingDimensions);

public sealed record StoredChunk(
    string Id,
    string TenantId,
    string DocumentId,
    string Title,
    string? RequiredRole,
    string SourceType,
    string? FileName,
    int ChunkIndex,
    int? PageNumber,
    string? SectionTitle,
    string Text,
    float[] Embedding,
    DateTimeOffset CreatedAt);
