using LocalRagLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LocalRagLab.Api.Controllers;

[ApiController]
[Route("api/traces")]
public sealed class TracesController : ControllerBase
{
    private readonly IRagTraceStore _traceStore;

    public TracesController(IRagTraceStore traceStore)
    {
        _traceStore = traceStore;
    }

    /// <summary>Returns recent complete RAG traces, including prompt and stage timings.</summary>
    [HttpGet]
    public IActionResult GetRecent([FromQuery] int count = 20) =>
        Ok(_traceStore.GetRecent(count));

    /// <summary>Returns one trace by the traceId returned from /api/rag/ask.</summary>
    [HttpGet("{traceId}")]
    public IActionResult Get(string traceId)
    {
        var trace = _traceStore.Get(traceId);
        return trace is null ? NotFound() : Ok(trace);
    }

    /// <summary>Clears debug traces only. Documents remain loaded.</summary>
    [HttpDelete]
    public IActionResult Clear()
    {
        _traceStore.Clear();
        return NoContent();
    }
}
