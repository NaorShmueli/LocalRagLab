namespace LocalRagLab.Api.Contracts;

public sealed record OllamaModelResponse(
    string Name,
    long? SizeBytes,
    DateTimeOffset? ModifiedAt);

public sealed record OllamaStatusResponse(
    bool Reachable,
    string BaseUrl,
    string ConfiguredChatModel,
    string ConfiguredEmbeddingModel,
    bool ChatModelInstalled,
    bool EmbeddingModelInstalled,
    IReadOnlyList<OllamaModelResponse> InstalledModels,
    string? Error);

public sealed record WarmupResponse(
    string ChatModel,
    string EmbeddingModel,
    int EmbeddingDimensions,
    string ChatReply,
    double TotalMilliseconds);
