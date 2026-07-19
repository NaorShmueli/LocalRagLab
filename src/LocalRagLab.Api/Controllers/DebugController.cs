using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace LocalRagLab.Api.Controllers;

[ApiController]
[Route("api/debug")]
public sealed class DebugController : ControllerBase
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IChatClient _chatClient;

    public DebugController(
        IEmbeddingClient embeddingClient,
        IChatClient chatClient)
    {
        _embeddingClient = embeddingClient;
        _chatClient = chatClient;
    }

    /// <summary>Creates one local embedding so its dimensions and values can be inspected.</summary>
    [HttpPost("embedding")]
    public async Task<ActionResult<EmbeddingDebugResponse>> Embedding(
        [FromBody] EmbeddingDebugRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _embeddingClient.CreateEmbeddingAsync(
            request.Text,
            request.Purpose,
            cancellationToken);

        return Ok(new EmbeddingDebugResponse(
            Model: result.Model,
            Purpose: request.Purpose,
            Dimensions: result.Vector.Length,
            Preview: result.Vector.Take(12).ToArray(),
            FullVector: request.IncludeFullVector ? result.Vector : null,
            Usage: result.Usage));
    }

    /// <summary>
    /// Embeds two texts locally and calculates cosine similarity. Useful for seeing how
    /// semantically similar sentences can be close even when they use different words.
    /// </summary>
    [HttpPost("similarity")]
    public async Task<ActionResult<SimilarityDebugResponse>> Similarity(
        [FromBody] SimilarityDebugRequest request,
        CancellationToken cancellationToken)
    {
        var embeddingA = await _embeddingClient.CreateEmbeddingAsync(
            request.TextA,
            request.PurposeA,
            cancellationToken);

        var embeddingB = await _embeddingClient.CreateEmbeddingAsync(
            request.TextB,
            request.PurposeB,
            cancellationToken);

        if (embeddingA.Vector.Length != embeddingB.Vector.Length)
        {
            throw new InvalidOperationException(
                "The two embedding vectors have different dimensions.");
        }

        return Ok(new SimilarityDebugResponse(
            Model: embeddingA.Model,
            Dimensions: embeddingA.Vector.Length,
            CosineSimilarity: CosineSimilarity(
                embeddingA.Vector,
                embeddingB.Vector),
            VectorAPreview: embeddingA.Vector.Take(12).ToArray(),
            VectorBPreview: embeddingB.Vector.Take(12).ToArray()));
    }

    /// <summary>Calls the local chat model directly, without retrieval or RAG.</summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ChatDebugResponse>> Chat(
        [FromBody] ChatDebugRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _chatClient.CompleteAsync(
            new[]
            {
                new ChatMessage("system", request.SystemPrompt),
                new ChatMessage("user", request.UserPrompt)
            },
            request.Temperature,
            cancellationToken);

        return Ok(new ChatDebugResponse(
            Model: result.Model,
            Content: result.Content,
            Usage: result.Usage));
    }

    private static double CosineSimilarity(
        IReadOnlyList<float> left,
        IReadOnlyList<float> right)
    {
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
