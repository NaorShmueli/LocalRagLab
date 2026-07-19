using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalRagLab.Api.Controllers;

[ApiController]
[Route("api/rag")]
public sealed class RagController : ControllerBase
{
    private readonly RagQueryService _ragQueryService;

    public RagController(RagQueryService ragQueryService)
    {
        _ragQueryService = ragQueryService;
    }

    /// <summary>
    /// Executes the complete local RAG pipeline: query embedding, authorized retrieval,
    /// debug reranking, prompt construction, Ollama generation, citations, and optional
    /// LLM-as-a-judge groundedness evaluation.
    /// </summary>
    [HttpPost("ask")]
    [ProducesResponseType<AskRagResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AskRagResponse>> Ask(
        [FromBody] AskRagRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _ragQueryService.AskAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}
