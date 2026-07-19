using System.ComponentModel.DataAnnotations;
using LocalRagLab.Api.Domain;

namespace LocalRagLab.Api.Contracts;

public sealed class EmbeddingDebugRequest
{
    [Required]
    public string Text { get; init; } = string.Empty;

    public EmbeddingPurpose Purpose { get; init; } = EmbeddingPurpose.Query;

    public bool IncludeFullVector { get; init; }
}

public sealed record EmbeddingDebugResponse(
    string Model,
    EmbeddingPurpose Purpose,
    int Dimensions,
    IReadOnlyList<float> Preview,
    IReadOnlyList<float>? FullVector,
    ModelUsage Usage);

public sealed class SimilarityDebugRequest
{
    [Required]
    public string TextA { get; init; } = string.Empty;

    [Required]
    public string TextB { get; init; } = string.Empty;

    public EmbeddingPurpose PurposeA { get; init; } = EmbeddingPurpose.Query;
    public EmbeddingPurpose PurposeB { get; init; } = EmbeddingPurpose.Document;
}

public sealed record SimilarityDebugResponse(
    string Model,
    int Dimensions,
    double CosineSimilarity,
    IReadOnlyList<float> VectorAPreview,
    IReadOnlyList<float> VectorBPreview);

public sealed class ChatDebugRequest
{
    public string SystemPrompt { get; init; } = "You are a helpful assistant.";

    [Required]
    public string UserPrompt { get; init; } = string.Empty;

    [Range(0, 2)]
    public double? Temperature { get; init; }
}

public sealed record ChatDebugResponse(
    string Model,
    string Content,
    ModelUsage Usage);
