using System.Text.Json;
using System.Text.Json.Serialization;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Infrastructure;

namespace LocalRagLab.Api.Services;

public interface IGroundednessEvaluator
{
    Task<GroundednessResult> EvaluateAsync(
        string question,
        string answer,
        IReadOnlyList<PromptSource> sources,
        CancellationToken cancellationToken);
}

public sealed class OllamaGroundednessEvaluator : IGroundednessEvaluator
{
    private readonly IChatClient _chatClient;

    public OllamaGroundednessEvaluator(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<GroundednessResult> EvaluateAsync(
        string question,
        string answer,
        IReadOnlyList<PromptSource> sources,
        CancellationToken cancellationToken)
    {
        var sourceText = string.Join(
            "\n\n",
            sources.Select(source => $"[{source.Label}] {source.Text}"));

        var messages = new[]
        {
            new ChatMessage(
                "system",
                """
                You evaluate whether an answer is fully supported by provided sources.
                Return ONLY valid JSON using this exact schema:
                {
                  "isGrounded": true,
                  "score": 0.0,
                  "explanation": "short explanation",
                  "unsupportedClaims": ["claim"]
                }
                Score must be between 0 and 1. Do not use outside knowledge.
                """),
            new ChatMessage(
                "user",
                $"""
                QUESTION:
                {question}

                SOURCES:
                {sourceText}

                ANSWER TO EVALUATE:
                {answer}
                """)
        };

        try
        {
            var result = await _chatClient.CompleteAsync(
                messages,
                temperature: 0,
                cancellationToken);

            var json = ExtractJsonObject(result.Content);
            var parsed = JsonSerializer.Deserialize<GroundednessPayload>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                });

            if (parsed is null)
            {
                return ParseFailure(result.Content, "The evaluator returned empty JSON.");
            }

            return new GroundednessResult(
                EvaluationCompleted: true,
                IsGrounded: parsed.IsGrounded,
                Score: Math.Clamp(parsed.Score, 0, 1),
                Explanation: parsed.Explanation ?? string.Empty,
                UnsupportedClaims: parsed.UnsupportedClaims ?? Array.Empty<string>(),
                RawModelOutput: result.Content);
        }
        catch (Exception exception) when (exception is JsonException or OllamaException)
        {
            return ParseFailure(
                raw: exception is OllamaException ? null : exception.Message,
                explanation: $"Groundedness evaluation failed: {exception.Message}");
        }
    }

    private static GroundednessResult ParseFailure(string? raw, string explanation) => new(
        EvaluationCompleted: false,
        IsGrounded: null,
        Score: null,
        Explanation: explanation,
        UnsupportedClaims: Array.Empty<string>(),
        RawModelOutput: raw);

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            throw new JsonException("No JSON object was found in evaluator output.");
        }

        return text[start..(end + 1)];
    }

    private sealed record GroundednessPayload(
        [property: JsonPropertyName("isGrounded")] bool IsGrounded,
        [property: JsonPropertyName("score")] double Score,
        [property: JsonPropertyName("explanation")] string? Explanation,
        [property: JsonPropertyName("unsupportedClaims")] IReadOnlyList<string>? UnsupportedClaims);
}
