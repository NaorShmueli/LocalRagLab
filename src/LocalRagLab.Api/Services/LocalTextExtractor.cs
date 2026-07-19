using System.Text;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Options;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace LocalRagLab.Api.Services;

public interface ITextExtractor
{
    Task<ExtractedDocument> ExtractAsync(
        IFormFile file,
        CancellationToken cancellationToken);
}

public sealed class LocalTextExtractor : ITextExtractor
{
    private static readonly HashSet<string> TextExtensions = new(
        new[] { ".txt", ".md", ".markdown", ".json", ".csv" },
        StringComparer.OrdinalIgnoreCase);

    private readonly RagOptions _options;

    public LocalTextExtractor(IOptions<RagOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ExtractedDocument> ExtractAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new ArgumentException("Uploaded file is empty.");
        }

        if (file.Length > _options.MaxUploadBytes)
        {
            throw new ArgumentException(
                $"Uploaded file exceeds the {_options.MaxUploadBytes} byte limit.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return await ExtractPdfAsync(file, cancellationToken);
        }

        if (TextExtensions.Contains(extension))
        {
            return await ExtractTextAsync(file, cancellationToken);
        }

        throw new ArgumentException(
            "Unsupported file type. Use PDF, TXT, MD, JSON, or CSV for this learning lab.");
    }

    private static async Task<ExtractedDocument> ExtractTextAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);

        var text = await reader.ReadToEndAsync(cancellationToken);

        return new ExtractedDocument(
            FileName: file.FileName,
            MediaType: file.ContentType ?? "text/plain",
            Segments: new[]
            {
                new SourceSegment(
                    PageNumber: null,
                    SectionTitle: null,
                    Text: text)
            });
    }

    private static Task<ExtractedDocument> ExtractPdfAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var stream = file.OpenReadStream();
        using var document = PdfDocument.Open(stream);

        var segments = new List<SourceSegment>(document.NumberOfPages);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                segments.Add(new SourceSegment(
                    PageNumber: page.Number,
                    SectionTitle: $"Page {page.Number}",
                    Text: page.Text));
            }
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException(
                "No text could be extracted from the PDF. It may be a scanned PDF that requires OCR, which is intentionally not hidden inside this lab.");
        }

        return Task.FromResult(new ExtractedDocument(
            FileName: file.FileName,
            MediaType: file.ContentType ?? "application/pdf",
            Segments: segments));
    }
}
