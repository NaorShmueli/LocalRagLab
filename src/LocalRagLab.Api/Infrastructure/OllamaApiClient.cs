using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Options;
using Microsoft.Extensions.Options;

namespace LocalRagLab.Api.Infrastructure;

public interface IEmbeddingClient
{
    Task<EmbeddingResult> CreateEmbeddingAsync(
        string text,
        EmbeddingPurpose purpose,
        CancellationToken cancellationToken);
}

public interface IChatClient
{
    Task<ChatCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        double? temperature,
        CancellationToken cancellationToken);
}

public interface IOllamaDiagnostics
{
    Task<IReadOnlyList<OllamaModelResponse>> GetInstalledModelsAsync(
        CancellationToken cancellationToken);
}

public sealed record ChatMessage(string Role, string Content);

public sealed class OllamaApiClient : IEmbeddingClient, IChatClient, IOllamaDiagnostics
{
    public const string HttpClientName = "Ollama";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaApiClient> _logger;

    public OllamaApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OllamaOptions> options,
        ILogger<OllamaApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmbeddingResult> CreateEmbeddingAsync(
        string text,
        EmbeddingPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Embedding input cannot be empty.", nameof(text));
        }

        var preparedInput = PrepareEmbeddingInput(text, purpose);
        var request = new OllamaEmbedRequest(
            Model: _options.EmbeddingModel,
            Input: preparedInput,
            Truncate: true,
            KeepAlive: _options.KeepAlive);

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(
            "api/embed",
            request,
            cancellationToken);

        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new OllamaException(
                $"Ollama embedding request failed with HTTP {(int)response.StatusCode}: {rawBody}");
        }

        var payload = JsonSerializer.Deserialize<OllamaEmbedResponse>(
            rawBody,
            JsonDefaults.Options)
            ?? throw new OllamaException("Ollama returned an empty embedding response.");

        var embedding = payload.Embeddings.FirstOrDefault()
            ?? throw new OllamaException("Ollama did not return an embedding vector.");

        Normalize(embedding);

        _logger.LogDebug(
            "Created {Dimensions}-dimension embedding using {Model} for {Purpose}.",
            embedding.Length,
            _options.EmbeddingModel,
            purpose);

        return new EmbeddingResult(
            Vector: embedding,
            Model: payload.Model ?? _options.EmbeddingModel,
            Usage: ToUsage(payload));
    }

    public async Task<ChatCompletionResult> CompleteAsync(
        IReadOnlyList<ChatMessage> messages,
        double? temperature,
        CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            throw new ArgumentException("At least one chat message is required.", nameof(messages));
        }

        var request = new OllamaChatRequest(
            Model: _options.ChatModel,
            Messages: messages.Select(message => new OllamaMessage(
                message.Role,
                message.Content)).ToArray(),
            Stream: false,
            KeepAlive: _options.KeepAlive,
            Options: new OllamaGenerationOptions(
                Temperature: temperature ?? _options.Temperature));

        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.PostAsJsonAsync(
            "api/chat",
            request,
            cancellationToken);

        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new OllamaException(
                $"Ollama chat request failed with HTTP {(int)response.StatusCode}: {rawBody}");
        }

        var payload = JsonSerializer.Deserialize<OllamaChatResponse>(
            rawBody,
            JsonDefaults.Options)
            ?? throw new OllamaException("Ollama returned an empty chat response.");

        if (string.IsNullOrWhiteSpace(payload.Message?.Content))
        {
            throw new OllamaException("Ollama returned a chat response without content.");
        }

        return new ChatCompletionResult(
            Content: payload.Message.Content.Trim(),
            Model: payload.Model ?? _options.ChatModel,
            Usage: ToUsage(payload));
    }

    public async Task<IReadOnlyList<OllamaModelResponse>> GetInstalledModelsAsync(
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        using var response = await client.GetAsync("api/tags", cancellationToken);
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new OllamaException(
                $"Ollama tags request failed with HTTP {(int)response.StatusCode}: {rawBody}");
        }

        var payload = JsonSerializer.Deserialize<OllamaTagsResponse>(
            rawBody,
            JsonDefaults.Options)
            ?? new OllamaTagsResponse(Array.Empty<OllamaModel>());

        return payload.Models
            .Select(model => new OllamaModelResponse(
                Name: model.Name,
                SizeBytes: model.Size,
                ModifiedAt: model.ModifiedAt))
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string PrepareEmbeddingInput(string text, EmbeddingPurpose purpose)
    {
        var trimmed = text.Trim();

        if (!_options.UseNomicSearchPrefixes)
        {
            return trimmed;
        }

        return purpose == EmbeddingPurpose.Query
            ? $"search_query: {trimmed}"
            : $"search_document: {trimmed}";
    }

    private static void Normalize(float[] vector)
    {
        double sumOfSquares = 0;

        foreach (var value in vector)
        {
            sumOfSquares += value * value;
        }

        var length = Math.Sqrt(sumOfSquares);
        if (length <= double.Epsilon)
        {
            return;
        }

        for (var index = 0; index < vector.Length; index++)
        {
            vector[index] = (float)(vector[index] / length);
        }
    }

    private static ModelUsage ToUsage(OllamaMetricResponse response) => new(
        TotalDurationNanoseconds: response.TotalDuration,
        LoadDurationNanoseconds: response.LoadDuration,
        PromptEvaluationCount: response.PromptEvalCount,
        EvaluationCount: response.EvalCount,
        PromptEvaluationDurationNanoseconds: response.PromptEvalDuration,
        EvaluationDurationNanoseconds: response.EvalDuration);

    private sealed record OllamaEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("truncate")] bool Truncate,
        [property: JsonPropertyName("keep_alive")] string KeepAlive);

    private sealed record OllamaEmbedResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("embeddings")] float[][] Embeddings,
        [property: JsonPropertyName("total_duration")] long? TotalDuration,
        [property: JsonPropertyName("load_duration")] long? LoadDuration,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int? EvalCount,
        [property: JsonPropertyName("prompt_eval_duration")] long? PromptEvalDuration,
        [property: JsonPropertyName("eval_duration")] long? EvalDuration)
        : OllamaMetricResponse(
            TotalDuration,
            LoadDuration,
            PromptEvalCount,
            EvalCount,
            PromptEvalDuration,
            EvalDuration);

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("keep_alive")] string KeepAlive,
        [property: JsonPropertyName("options")] OllamaGenerationOptions Options);

    private sealed record OllamaGenerationOptions(
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("message")] OllamaMessage? Message,
        [property: JsonPropertyName("total_duration")] long? TotalDuration,
        [property: JsonPropertyName("load_duration")] long? LoadDuration,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int? EvalCount,
        [property: JsonPropertyName("prompt_eval_duration")] long? PromptEvalDuration,
        [property: JsonPropertyName("eval_duration")] long? EvalDuration)
        : OllamaMetricResponse(
            TotalDuration,
            LoadDuration,
            PromptEvalCount,
            EvalCount,
            PromptEvalDuration,
            EvalDuration);

    private abstract record OllamaMetricResponse(
        long? TotalDuration,
        long? LoadDuration,
        int? PromptEvalCount,
        int? EvalCount,
        long? PromptEvalDuration,
        long? EvalDuration);

    private sealed record OllamaTagsResponse(
        [property: JsonPropertyName("models")] IReadOnlyList<OllamaModel> Models);

    private sealed record OllamaModel(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("size")] long? Size,
        [property: JsonPropertyName("modified_at")] DateTimeOffset? ModifiedAt);

    private static class JsonDefaults
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
    }
}

public sealed class OllamaException : Exception
{
    public OllamaException(string message) : base(message)
    {
    }
}
