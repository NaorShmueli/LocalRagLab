using System.Collections.Concurrent;
using LocalRagLab.Api.Domain;

namespace LocalRagLab.Api.Infrastructure;

public interface IVectorStore
{
    Task ReplaceDocumentAsync(
        DocumentInfo document,
        IReadOnlyCollection<StoredChunk> chunks,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentInfo>> ListDocumentsAsync(
        string? tenantId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StoredChunk>> GetDocumentChunksAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VectorCandidate>> SearchAsync(
        string tenantId,
        IReadOnlyCollection<string> roles,
        float[] queryEmbedding,
        int topK,
        double minimumSimilarity,
        CancellationToken cancellationToken);

    Task<bool> DeleteDocumentAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly ConcurrentDictionary<string, DocumentInfo> _documents = new();
    private readonly ConcurrentDictionary<string, StoredChunk> _chunks = new();
    private readonly object _writeLock = new();

    public Task ReplaceDocumentAsync(
        DocumentInfo document,
        IReadOnlyCollection<StoredChunk> chunks,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_writeLock)
        {
            var documentKey = DocumentKey(document.TenantId, document.DocumentId);

            foreach (var existingChunk in _chunks.Values.Where(chunk =>
                         string.Equals(chunk.TenantId, document.TenantId, StringComparison.Ordinal) &&
                         string.Equals(chunk.DocumentId, document.DocumentId, StringComparison.Ordinal)))
            {
                _chunks.TryRemove(existingChunk.Id, out _);
            }

            foreach (var chunk in chunks)
            {
                _chunks[chunk.Id] = chunk;
            }

            _documents[documentKey] = document;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DocumentInfo>> ListDocumentsAsync(
        string? tenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var documents = _documents.Values
            .Where(document => tenantId is null ||
                string.Equals(document.TenantId, tenantId, StringComparison.Ordinal))
            .OrderBy(document => document.TenantId, StringComparer.Ordinal)
            .ThenBy(document => document.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<DocumentInfo>>(documents);
    }

    public Task<IReadOnlyList<StoredChunk>> GetDocumentChunksAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var chunks = _chunks.Values
            .Where(chunk =>
                string.Equals(chunk.TenantId, tenantId, StringComparison.Ordinal) &&
                string.Equals(chunk.DocumentId, documentId, StringComparison.Ordinal))
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToArray();

        return Task.FromResult<IReadOnlyList<StoredChunk>>(chunks);
    }

    public Task<IReadOnlyList<VectorCandidate>> SearchAsync(
        string tenantId,
        IReadOnlyCollection<string> roles,
        float[] queryEmbedding,
        int topK,
        double minimumSimilarity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedRoles = new HashSet<string>(
            roles,
            StringComparer.OrdinalIgnoreCase);

        // Important: tenant and authorization filters are applied BEFORE the
        // candidate texts leave the store.
        var candidates = _chunks.Values
            .Where(chunk => string.Equals(
                chunk.TenantId,
                tenantId,
                StringComparison.Ordinal))
            .Where(chunk => string.IsNullOrWhiteSpace(chunk.RequiredRole) ||
                normalizedRoles.Contains(chunk.RequiredRole))
            .Select(chunk => new VectorCandidate(
                Chunk: chunk,
                SimilarityScore: CosineSimilarity(
                    queryEmbedding,
                    chunk.Embedding)))
            .Where(candidate => candidate.SimilarityScore >= minimumSimilarity)
            .OrderByDescending(candidate => candidate.SimilarityScore)
            .Take(topK)
            .ToArray();

        return Task.FromResult<IReadOnlyList<VectorCandidate>>(candidates);
    }

    public Task<bool> DeleteDocumentAsync(
        string tenantId,
        string documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var removed = false;

        lock (_writeLock)
        {
            removed = _documents.TryRemove(DocumentKey(tenantId, documentId), out _);

            foreach (var chunk in _chunks.Values.Where(chunk =>
                         string.Equals(chunk.TenantId, tenantId, StringComparison.Ordinal) &&
                         string.Equals(chunk.DocumentId, documentId, StringComparison.Ordinal)))
            {
                _chunks.TryRemove(chunk.Id, out _);
            }
        }

        return Task.FromResult(removed);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _documents.Clear();
        _chunks.Clear();
        return Task.CompletedTask;
    }

    private static string DocumentKey(string tenantId, string documentId) =>
        $"{tenantId}::{documentId}";

    private static double CosineSimilarity(
        IReadOnlyList<float> left,
        IReadOnlyList<float> right)
    {
        if (left.Count != right.Count)
        {
            return -1;
        }

        double dotProduct = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var index = 0; index < left.Count; index++)
        {
            dotProduct += left[index] * right[index];
            leftMagnitude += left[index] * left[index];
            rightMagnitude += right[index] * right[index];
        }

        if (leftMagnitude <= double.Epsilon || rightMagnitude <= double.Epsilon)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }
}
