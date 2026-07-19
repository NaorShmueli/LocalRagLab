using System.Text.RegularExpressions;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Options;
using Microsoft.Extensions.Options;

namespace LocalRagLab.Api.Services;

public interface ITextChunker
{
    IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<SourceSegment> segments);
}

public sealed class NaturalBoundaryTextChunker : ITextChunker
{
    private readonly RagOptions _options;

    public NaturalBoundaryTextChunker(IOptions<RagOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<SourceSegment> segments)
    {
        if (_options.ChunkOverlapCharacters >= _options.ChunkSizeCharacters)
        {
            throw new InvalidOperationException(
                "ChunkOverlapCharacters must be smaller than ChunkSizeCharacters.");
        }

        var chunks = new List<ChunkDraft>();
        var globalIndex = 0;

        foreach (var segment in segments)
        {
            var normalized = Normalize(segment.Text);
            var start = 0;

            while (start < normalized.Length)
            {
                var proposedEnd = Math.Min(
                    start + _options.ChunkSizeCharacters,
                    normalized.Length);

                var actualEnd = FindNaturalBoundary(
                    normalized,
                    start,
                    proposedEnd);

                if (actualEnd <= start)
                {
                    actualEnd = proposedEnd;
                }

                var text = normalized[start..actualEnd].Trim();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    chunks.Add(new ChunkDraft(
                        ChunkIndex: globalIndex++,
                        PageNumber: segment.PageNumber,
                        SectionTitle: segment.SectionTitle,
                        Text: text));
                }

                if (actualEnd >= normalized.Length)
                {
                    break;
                }

                start = Math.Max(
                    start + 1,
                    actualEnd - _options.ChunkOverlapCharacters);
            }
        }

        return chunks;
    }

    private static string Normalize(string text)
    {
        var normalizedLineEndings = Regex.Replace(text, @"\r\n?", "\n");
        var collapsedSpaces = Regex.Replace(normalizedLineEndings, @"[\t ]+", " ");
        return Regex.Replace(collapsedSpaces, @"\n{3,}", "\n\n").Trim();
    }

    private static int FindNaturalBoundary(
        string text,
        int start,
        int proposedEnd)
    {
        if (proposedEnd >= text.Length)
        {
            return text.Length;
        }

        var minimumBoundary = start + (int)((proposedEnd - start) * 0.6);

        // Prefer paragraph boundaries, then sentence boundaries, then whitespace.
        for (var index = proposedEnd - 1; index >= minimumBoundary; index--)
        {
            if (index > 0 && text[index] == '\n' && text[index - 1] == '\n')
            {
                return index + 1;
            }
        }

        for (var index = proposedEnd - 1; index >= minimumBoundary; index--)
        {
            if (text[index] is '.' or '!' or '?' or ';' or ':')
            {
                return index + 1;
            }
        }

        for (var index = proposedEnd - 1; index >= minimumBoundary; index--)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                return index + 1;
            }
        }

        return proposedEnd;
    }
}
