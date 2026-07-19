using System.Text.RegularExpressions;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Options;
using Microsoft.Extensions.Options;

namespace LocalRagLab.Api.Services;

public interface IReranker
{
    IReadOnlyList<RerankedCandidate> Rerank(
        string query,
        IReadOnlyList<VectorCandidate> candidates);
}

public sealed class HybridDebugReranker : IReranker
{
    private static readonly HashSet<string> StopWords = new(
        new[]
        {
            "the", "a", "an", "is", "are", "was", "were", "to", "of",
            "in", "on", "and", "or", "for", "with", "how", "what", "can",
            "could", "should", "i", "we", "you", "it", "this", "that",
            "של", "את", "על", "עם", "האם", "איך", "מה", "זה", "זו", "אני"
        },
        StringComparer.OrdinalIgnoreCase);

    private readonly RagOptions _options;

    public HybridDebugReranker(IOptions<RagOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<RerankedCandidate> Rerank(
        string query,
        IReadOnlyList<VectorCandidate> candidates)
    {
        var queryTokens = Tokenize(query);

        var scored = candidates
            .GroupBy(candidate => candidate.Chunk.Text, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(candidate =>
            {
                var lexicalScore = CalculateLexicalScore(
                    queryTokens,
                    Tokenize(candidate.Chunk.Text));

                var finalScore =
                    (_options.SemanticWeight * candidate.SimilarityScore) +
                    (_options.LexicalWeight * lexicalScore);

                return new
                {
                    candidate.Chunk,
                    candidate.SimilarityScore,
                    LexicalScore = lexicalScore,
                    FinalScore = finalScore
                };
            })
            .OrderByDescending(item => item.FinalScore)
            .ToArray();

        return scored
            .Select((item, index) => new RerankedCandidate(
                Chunk: item.Chunk,
                SimilarityScore: item.SimilarityScore,
                LexicalScore: item.LexicalScore,
                FinalScore: item.FinalScore,
                IsSelected: index < _options.ContextCount))
            .ToArray();
    }

    private static HashSet<string> Tokenize(string text) =>
        Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]+")
            .Select(match => match.Value)
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static double CalculateLexicalScore(
        IReadOnlySet<string> queryTokens,
        IReadOnlySet<string> documentTokens)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var matches = queryTokens.Count(documentTokens.Contains);
        return (double)matches / queryTokens.Count;
    }
}
