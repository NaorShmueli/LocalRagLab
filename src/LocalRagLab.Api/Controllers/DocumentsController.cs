using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Infrastructure;
using LocalRagLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalRagLab.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly DocumentIngestionService _ingestionService;
    private readonly IVectorStore _vectorStore;

    public DocumentsController(
        DocumentIngestionService ingestionService,
        IVectorStore vectorStore)
    {
        _ingestionService = ingestionService;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Ingests raw text, chunks it, creates local embeddings, and stores the chunks in memory.
    /// Reusing the same tenant/document ID replaces the previous document.
    /// </summary>
    [HttpPost("text")]
    [ProducesResponseType<IngestDocumentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IngestDocumentResponse>> IngestText(
        [FromBody] IngestTextRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ingestionService.IngestTextAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Uploads a PDF/TXT/MD/JSON/CSV document. PDF text is extracted locally using PdfPig.
    /// Scanned image PDFs require OCR and intentionally return a clear error.
    /// </summary>
    [HttpPost("file")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<IngestDocumentResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IngestDocumentResponse>> IngestFile(
        [FromForm] UploadDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ingestionService.IngestFileAsync(
            request,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>Lists the documents currently held in the in-memory store.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        var documents = await _vectorStore.ListDocumentsAsync(
            tenantId,
            cancellationToken);

        return Ok(documents);
    }

    /// <summary>
    /// Shows every stored chunk. Set includeEmbedding=true to inspect the full vector.
    /// </summary>
    [HttpGet("{documentId}/chunks")]
    public async Task<IActionResult> GetChunks(
        string documentId,
        [FromQuery] string tenantId,
        [FromQuery] bool includeEmbedding = false,
        CancellationToken cancellationToken = default)
    {
        var chunks = await _vectorStore.GetDocumentChunksAsync(
            tenantId,
            documentId,
            cancellationToken);

        return Ok(chunks.Select(chunk => new DocumentChunkResponse(
            ChunkId: chunk.Id,
            TenantId: chunk.TenantId,
            DocumentId: chunk.DocumentId,
            Title: chunk.Title,
            RequiredRole: chunk.RequiredRole,
            ChunkIndex: chunk.ChunkIndex,
            PageNumber: chunk.PageNumber,
            SectionTitle: chunk.SectionTitle,
            Text: chunk.Text,
            EmbeddingDimensions: chunk.Embedding.Length,
            Embedding: includeEmbedding ? chunk.Embedding : null)));
    }

    /// <summary>Deletes one document and all of its vectors from memory.</summary>
    [HttpDelete("{documentId}")]
    public async Task<IActionResult> Delete(
        string documentId,
        [FromQuery] string tenantId,
        CancellationToken cancellationToken)
    {
        var removed = await _vectorStore.DeleteDocumentAsync(
            tenantId,
            documentId,
            cancellationToken);

        return removed ? NoContent() : NotFound();
    }

    /// <summary>Clears all in-memory documents and vectors.</summary>
    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await _vectorStore.ClearAsync(cancellationToken);
        return NoContent();
    }
}
