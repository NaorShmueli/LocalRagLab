using System.Diagnostics;
using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Infrastructure;
using LocalRagLab.Api.Options;
using Microsoft.Extensions.Options;

namespace LocalRagLab.Api.Services;

public sealed class SemanticSearchService
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly RagOptions _options;

    public SemanticSearchService(
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        IOptions<RagOptions> options)
    {
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _options = options.Value;
    }

    public async Task<SemanticSearchResponse> SearchAsync(
        SemanticSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ArgumentException("Query is required.");
        }

        var embeddingStopwatch = Stopwatch.StartNew();
        var embedding = await _embeddingClient.CreateEmbeddingAsync(
            request.Query,
            EmbeddingPurpose.Query,
            cancellationToken);
        embeddingStopwatch.Stop();

        var searchStopwatch = Stopwatch.StartNew();
        var candidates = await _vectorStore.SearchAsync(
            tenantId: request.TenantId,
            roles: request.Roles,
            queryEmbedding: embedding.Vector,
            topK: request.TopK ?? _options.CandidateCount,
            minimumSimilarity: request.MinimumSimilarity ?? _options.MinimumSimilarity,
            cancellationToken: cancellationToken);
        searchStopwatch.Stop();

        return new SemanticSearchResponse(
            Query: request.Query,
            EmbeddingModel: embedding.Model,
            EmbeddingDimensions: embedding.Vector.Length,
            EmbeddingPreview: embedding.Vector.Take(12).ToArray(),
            Results: candidates.Select((candidate, index) => new SearchResultResponse(
                Rank: index + 1,
                ChunkId: candidate.Chunk.Id,
                DocumentId: candidate.Chunk.DocumentId,
                Title: candidate.Chunk.Title,
                ChunkIndex: candidate.Chunk.ChunkIndex,
                PageNumber: candidate.Chunk.PageNumber,
                SectionTitle: candidate.Chunk.SectionTitle,
                SimilarityScore: candidate.SimilarityScore,
                Text: candidate.Chunk.Text)).ToArray(),
            EmbeddingMilliseconds: embeddingStopwatch.Elapsed.TotalMilliseconds,
            SearchMilliseconds: searchStopwatch.Elapsed.TotalMilliseconds);
    }
}
