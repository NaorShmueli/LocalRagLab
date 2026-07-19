using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalRagLab.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly SemanticSearchService _searchService;

    public SearchController(SemanticSearchService searchService)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Runs only the embedding + cosine-similarity retrieval stages, without calling the chat LLM.
    /// Use this endpoint to debug retrieval separately from generation.
    /// </summary>
    [HttpPost("semantic")]
    [ProducesResponseType<SemanticSearchResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SemanticSearchResponse>> Search(
        [FromBody] SemanticSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _searchService.SearchAsync(
            request,
            cancellationToken);

        return Ok(result);
    }
}
