using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Infrastructure;

namespace LocalRagLab.Api.Services;

public sealed class DocumentIngestionService
{
    private readonly ITextExtractor _textExtractor;
    private readonly ITextChunker _chunker;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        ITextExtractor textExtractor,
        ITextChunker chunker,
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        ILogger<DocumentIngestionService> logger)
    {
        _textExtractor = textExtractor;
        _chunker = chunker;
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public Task<IngestDocumentResponse> IngestTextAsync(
        IngestTextRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMetadata(
            request.TenantId,
            request.DocumentId,
            request.Title);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("Text is required.");
        }

        var segments = new[]
        {
            new SourceSegment(
                PageNumber: null,
                SectionTitle: null,
                Text: request.Text)
        };

        return IngestAsync(
            tenantId: request.TenantId,
            documentId: request.DocumentId,
            title: request.Title,
            requiredRole: request.RequiredRole,
            sourceType: "text",
            fileName: null,
            segments: segments,
            cancellationToken: cancellationToken);
    }

    public async Task<IngestDocumentResponse> IngestFileAsync(
        UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateMetadata(
            request.TenantId,
            request.DocumentId,
            request.Title);

        var extractedDocument = await _textExtractor.ExtractAsync(
            request.File,
            cancellationToken);

        return await IngestAsync(
            tenantId: request.TenantId,
            documentId: request.DocumentId,
            title: request.Title,
            requiredRole: request.RequiredRole,
            sourceType: extractedDocument.MediaType,
            fileName: extractedDocument.FileName,
            segments: extractedDocument.Segments,
            cancellationToken: cancellationToken);
    }

    private async Task<IngestDocumentResponse> IngestAsync(
        string tenantId,
        string documentId,
        string title,
        string? requiredRole,
        string sourceType,
        string? fileName,
        IReadOnlyList<SourceSegment> segments,
        CancellationToken cancellationToken)
    {
        var drafts = _chunker.Chunk(segments);

        if (drafts.Count == 0)
        {
            throw new ArgumentException("The document did not produce any text chunks.");
        }

        var createdAt = DateTimeOffset.UtcNow;
        var storedChunks = new List<StoredChunk>(drafts.Count);

        // Sequential on purpose: easier to debug and observe in a learning lab.
        // A production ingestion worker can use batching and bounded parallelism.
        foreach (var draft in drafts)
        {
            var embedding = await _embeddingClient.CreateEmbeddingAsync(
                draft.Text,
                EmbeddingPurpose.Document,
                cancellationToken);

            storedChunks.Add(new StoredChunk(
                Id: $"{tenantId}:{documentId}:{draft.ChunkIndex}",
                TenantId: tenantId,
                DocumentId: documentId,
                Title: title,
                RequiredRole: NormalizeOptional(requiredRole),
                SourceType: sourceType,
                FileName: fileName,
                ChunkIndex: draft.ChunkIndex,
                PageNumber: draft.PageNumber,
                SectionTitle: draft.SectionTitle,
                Text: draft.Text,
                Embedding: embedding.Vector,
                CreatedAt: createdAt));
        }

        var document = new DocumentInfo(
            TenantId: tenantId,
            DocumentId: documentId,
            Title: title,
            RequiredRole: NormalizeOptional(requiredRole),
            SourceType: sourceType,
            FileName: fileName,
            CreatedAt: createdAt,
            ChunkCount: storedChunks.Count,
            EmbeddingDimensions: storedChunks[0].Embedding.Length);

        await _vectorStore.ReplaceDocumentAsync(
            document,
            storedChunks,
            cancellationToken);

        _logger.LogInformation(
            "Ingested document {DocumentId} for tenant {TenantId}: {ChunkCount} chunks, {Dimensions} embedding dimensions.",
            documentId,
            tenantId,
            storedChunks.Count,
            document.EmbeddingDimensions);

        return new IngestDocumentResponse(
            Document: document,
            Chunks: storedChunks.Select(ToSummary).ToArray());
    }

    private static ChunkSummaryResponse ToSummary(StoredChunk chunk) => new(
        ChunkId: chunk.Id,
        ChunkIndex: chunk.ChunkIndex,
        PageNumber: chunk.PageNumber,
        SectionTitle: chunk.SectionTitle,
        CharacterCount: chunk.Text.Length,
        EmbeddingDimensions: chunk.Embedding.Length,
        EmbeddingPreview: chunk.Embedding.Take(12).ToArray(),
        Text: chunk.Text);

    private static void ValidateMetadata(
        string tenantId,
        string documentId,
        string title)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException("TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("DocumentId is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.");
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
