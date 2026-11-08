using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TecNM.Api.Modules.Expedients;

public sealed record ImssDateSuggestion(string Status, DateOnly? SuggestedDate = null, int? PageNumber = null);

public static partial class ImssDateExtractionService
{
    [GeneratedRegex(@"(?:fecha\s+de\s+(?:expedici[oó]n|emisi[oó]n)|expedido\s+el)", RegexOptions.IgnoreCase)]
    private static partial Regex Label();

    [GeneratedRegex(@"(?<!\d)(?:\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{4}|\d{1,2}\s+de\s+[a-záéíóú]+\s+de\s+\d{4})(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex DatePattern();

    public static ImssDateSuggestion Extract(Stream stream, CancellationToken ct = default)
    {
        try
        {
            using var pdf = PdfDocument.Open(stream);
            if (pdf.NumberOfPages > 10) return new("unavailable");
            var pages = new List<string>();
            var characters = 0;
            foreach (var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var text = ContentOrderTextExtractor.GetText(page);
                characters += text.Length;
                if (characters > 200_000) return new("unavailable");
                pages.Add(text);
            }
            return FromText(pages);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception) { return new("unavailable"); }
    }

    public static ImssDateSuggestion FromText(IReadOnlyList<string> pages)
    {
        if (pages.Count > 10 || pages.Sum(p => p.Length) > 200_000) return new("unavailable");
        var candidates = new Dictionary<DateOnly, int>();
        var invalid = false;
        for (var page = 0; page < pages.Count; page++)
        {
            var lines = pages[page].Replace("\r", "").Split('\n');
            for (var line = 0; line < lines.Length; line++)
            {
                var label = Label().Match(lines[line]);
                if (!label.Success) continue;
                var following = lines[line][(label.Index + label.Length)..];
                var matches = DatePattern().Matches(following);
                if (matches.Count == 0 && line + 1 < lines.Length) matches = DatePattern().Matches(lines[line + 1]);
                foreach (Match match in matches)
                {
                    var value = Regex.Replace(match.Value.ToLowerInvariant(), @"\s+", " ");
                    if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "d/M/yyyy", "d 'de' MMMM 'de' yyyy"],
                        CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out var date)) candidates.TryAdd(date, page + 1);
                    else invalid = true;
                }
            }
        }
        if (candidates.Count > 1 || invalid) return new("ambiguous");
        return candidates.Count == 1 ? new("found", candidates.First().Key, candidates.First().Value) : new("not_found");
    }
}
