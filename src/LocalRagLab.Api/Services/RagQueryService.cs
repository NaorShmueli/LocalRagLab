using System.Diagnostics;
using LocalRagLab.Api.Contracts;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Infrastructure;
using LocalRagLab.Api.Options;
using Microsoft.Extensions.Options;

namespace LocalRagLab.Api.Services;

public sealed class RagQueryService
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IVectorStore _vectorStore;
    private readonly IReranker _reranker;
    private readonly IPromptBuilder _promptBuilder;
    private readonly IChatClient _chatClient;
    private readonly IGroundednessEvaluator _groundednessEvaluator;
    private readonly IRagTraceStore _traceStore;
    private readonly RagOptions _options;
    private readonly ILogger<RagQueryService> _logger;

    public RagQueryService(
        IEmbeddingClient embeddingClient,
        IVectorStore vectorStore,
        IReranker reranker,
        IPromptBuilder promptBuilder,
        IChatClient chatClient,
        IGroundednessEvaluator groundednessEvaluator,
        IRagTraceStore traceStore,
        IOptions<RagOptions> options,
        ILogger<RagQueryService> logger)
    {
        _embeddingClient = embeddingClient;
        _vectorStore = vectorStore;
        _reranker = reranker;
        _promptBuilder = promptBuilder;
        _chatClient = chatClient;
        _groundednessEvaluator = groundednessEvaluator;
        _traceStore = traceStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AskRagResponse> AskAsync(
        AskRagRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var traceId = Guid.NewGuid().ToString("N");
        var startedAt = DateTimeOffset.UtcNow;
        var totalStopwatch = Stopwatch.StartNew();
        var timings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        EmbeddingResult? embedding = null;
        IReadOnlyList<RerankedCandidate> reranked = Array.Empty<RerankedCandidate>();
        PromptPackage? prompt = null;
        ChatCompletionResult? chat = null;
        GroundednessResult? groundedness = null;
        var answer = string.Empty;
        var usedFallback = false;
        string? failure = null;

        try
        {
            var stage = Stopwatch.StartNew();
            embedding = await _embeddingClient.CreateEmbeddingAsync(
                request.Question,
                EmbeddingPurpose.Query,
                cancellationToken);
            stage.Stop();
            timings["queryEmbedding"] = stage.Elapsed.TotalMilliseconds;

            stage.Restart();
            var vectorCandidates = await _vectorStore.SearchAsync(
                tenantId: request.TenantId,
                roles: request.Roles,
                queryEmbedding: embedding.Vector,
                topK: _options.CandidateCount,
                minimumSimilarity: _options.MinimumSimilarity,
                cancellationToken: cancellationToken);
            stage.Stop();
            timings["vectorSearch"] = stage.Elapsed.TotalMilliseconds;

            stage.Restart();
            reranked = _reranker.Rerank(request.Question, vectorCandidates);
            stage.Stop();
            timings["reranking"] = stage.Elapsed.TotalMilliseconds;

            var selected = reranked.Where(candidate => candidate.IsSelected).ToArray();

            if (selected.Length == 0)
            {
                usedFallback = true;
                answer = "I do not have enough information in the authorized sources.";
            }
            else
            {
                stage.Restart();
                prompt = _promptBuilder.Build(request.Question, selected);
                stage.Stop();
                timings["promptConstruction"] = stage.Elapsed.TotalMilliseconds;

                stage.Restart();
                chat = await _chatClient.CompleteAsync(
                    new[]
                    {
                        new ChatMessage("system", prompt.SystemPrompt),
                        new ChatMessage("user", prompt.UserPrompt)
                    },
                    temperature: null,
                    cancellationToken);
                stage.Stop();
                timings["llmGeneration"] = stage.Elapsed.TotalMilliseconds;

                answer = chat.Content;

                if (request.EvaluateGroundedness)
                {
                    stage.Restart();
                    groundedness = await _groundednessEvaluator.EvaluateAsync(
                        request.Question,
                        answer,
                        prompt.Sources,
                        cancellationToken);
                    stage.Stop();
                    timings["groundednessEvaluation"] = stage.Elapsed.TotalMilliseconds;
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            throw;
        }
        finally
        {
            totalStopwatch.Stop();
            timings["total"] = totalStopwatch.Elapsed.TotalMilliseconds;

            var trace = BuildTrace(
                traceId,
                startedAt,
                request,
                embedding,
                reranked,
                prompt,
                chat,
                answer,
                usedFallback,
                groundedness,
                timings,
                failure);

            _traceStore.Add(trace);
        }

        var citations = prompt?.Sources
            .Select(source =>
            {
                var candidate = reranked.First(item => item.Chunk.Id == source.ChunkId);

                return new RagCitationResponse(
                    Label: source.Label,
                    ChunkId: source.ChunkId,
                    DocumentId: source.DocumentId,
                    Title: source.Title,
                    ChunkIndex: source.ChunkIndex,
                    PageNumber: source.PageNumber,
                    SectionTitle: source.SectionTitle,
                    RetrievalScore: candidate.FinalScore);
            })
            .ToArray()
            ?? Array.Empty<RagCitationResponse>();

        _logger.LogInformation(
            "RAG request {TraceId} completed in {ElapsedMilliseconds:F1} ms with {CandidateCount} candidates and {CitationCount} selected sources.",
            traceId,
            totalStopwatch.Elapsed.TotalMilliseconds,
            reranked.Count,
            citations.Length);

        return new AskRagResponse(
            TraceId: traceId,
            Answer: answer,
            UsedFallback: usedFallback,
            Citations: citations,
            Groundedness: groundedness,
            Debug: new RagDebugResponse(
                EmbeddingModel: embedding!.Model,
                QueryEmbeddingDimensions: embedding.Vector.Length,
                QueryEmbeddingPreview: embedding.Vector.Take(12).ToArray(),
                Candidates: BuildTraceCandidates(reranked),
                Prompt: prompt,
                ChatModel: chat?.Model,
                ChatUsage: chat?.Usage,
                StageMilliseconds: timings));
    }

    private static RagTraceRecord BuildTrace(
        string traceId,
        DateTimeOffset startedAt,
        AskRagRequest request,
        EmbeddingResult? embedding,
        IReadOnlyList<RerankedCandidate> reranked,
        PromptPackage? prompt,
        ChatCompletionResult? chat,
        string answer,
        bool usedFallback,
        GroundednessResult? groundedness,
        IReadOnlyDictionary<string, double> timings,
        string? failure) => new(
            TraceId: traceId,
            StartedAt: startedAt,
            CompletedAt: DateTimeOffset.UtcNow,
            TenantId: request.TenantId,
            UserId: request.UserId,
            Roles: request.Roles,
            Question: request.Question,
            EmbeddingModel: embedding?.Model ?? string.Empty,
            QueryEmbeddingDimensions: embedding?.Vector.Length ?? 0,
            QueryEmbeddingPreview: embedding?.Vector.Take(12).ToArray() ?? Array.Empty<float>(),
            Candidates: BuildTraceCandidates(reranked),
            Prompt: prompt,
            ChatModel: chat?.Model,
            ChatUsage: chat?.Usage,
            Answer: answer,
            UsedFallback: usedFallback,
            Groundedness: groundedness,
            StageMilliseconds: timings,
            Failure: failure);

    private static IReadOnlyList<RagTraceCandidate> BuildTraceCandidates(
        IReadOnlyList<RerankedCandidate> reranked) =>
        reranked.Select(candidate => new RagTraceCandidate(
            ChunkId: candidate.Chunk.Id,
            DocumentId: candidate.Chunk.DocumentId,
            Title: candidate.Chunk.Title,
            ChunkIndex: candidate.Chunk.ChunkIndex,
            PageNumber: candidate.Chunk.PageNumber,
            SimilarityScore: candidate.SimilarityScore,
            LexicalScore: candidate.LexicalScore,
            FinalScore: candidate.FinalScore,
            Selected: candidate.IsSelected,
            TextPreview: candidate.Chunk.Text.Length <= 300
                ? candidate.Chunk.Text
                : candidate.Chunk.Text[..300] + "..."))
            .ToArray();

    private static void Validate(AskRagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
        {
            throw new ArgumentException("TenantId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            throw new ArgumentException("UserId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ArgumentException("Question is required.");
        }
    }
}
