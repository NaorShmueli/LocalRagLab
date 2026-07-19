using System.Diagnostics;
using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Infrastructure;
using LocalRagLab.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LocalRagLab.Api.Controllers;

[ApiController]
[Route("api/system")]
public sealed class SystemController : ControllerBase
{
    private readonly IOllamaDiagnostics _diagnostics;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IChatClient _chatClient;
    private readonly OllamaOptions _options;

    public SystemController(
        IOllamaDiagnostics diagnostics,
        IEmbeddingClient embeddingClient,
        IChatClient chatClient,
        IOptions<OllamaOptions> options)
    {
        _diagnostics = diagnostics;
        _embeddingClient = embeddingClient;
        _chatClient = chatClient;
        _options = options.Value;
    }

    /// <summary>Checks whether Ollama is reachable and whether the configured models are installed.</summary>
    [HttpGet("ollama")]
    [ProducesResponseType<OllamaStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OllamaStatusResponse>> OllamaStatus(
        CancellationToken cancellationToken)
    {
        try
        {
            var models = await _diagnostics.GetInstalledModelsAsync(cancellationToken);
            var names = models.Select(model => model.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            return Ok(new OllamaStatusResponse(
                Reachable: true,
                BaseUrl: _options.BaseUrl,
                ConfiguredChatModel: _options.ChatModel,
                ConfiguredEmbeddingModel: _options.EmbeddingModel,
                ChatModelInstalled: names.Contains(_options.ChatModel),
                EmbeddingModelInstalled: names.Contains(_options.EmbeddingModel),
                InstalledModels: models,
                Error: null));
        }
        catch (OllamaException exception)
        {
            return Ok(new OllamaStatusResponse(
                Reachable: false,
                BaseUrl: _options.BaseUrl,
                ConfiguredChatModel: _options.ChatModel,
                ConfiguredEmbeddingModel: _options.EmbeddingModel,
                ChatModelInstalled: false,
                EmbeddingModelInstalled: false,
                InstalledModels: Array.Empty<OllamaModelResponse>(),
                Error: exception.Message));
        }
    }

    /// <summary>Loads both local models into memory and confirms that chat and embeddings work.</summary>
    [HttpPost("warmup")]
    [ProducesResponseType<WarmupResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<WarmupResponse>> Warmup(
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var embedding = await _embeddingClient.CreateEmbeddingAsync(
            "Local RAG warmup",
            EmbeddingPurpose.Query,
            cancellationToken);

        var chat = await _chatClient.CompleteAsync(
            new[]
            {
                new ChatMessage("user", "Reply with exactly: READY")
            },
            temperature: 0,
            cancellationToken);

        stopwatch.Stop();

        return Ok(new WarmupResponse(
            ChatModel: chat.Model,
            EmbeddingModel: embedding.Model,
            EmbeddingDimensions: embedding.Vector.Length,
            ChatReply: chat.Content,
            TotalMilliseconds: stopwatch.Elapsed.TotalMilliseconds));
    }
}
