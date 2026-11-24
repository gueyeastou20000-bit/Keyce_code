using System.Globalization;
using System.IO;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace KEYCE_KODER.App.Pdf;

/// <summary>Informations d'un PDF (fenêtre « Infos »).</summary>
public sealed record PdfInfo(
    int PageCount,
    string Version,
    string? Title,
    string? Author,
    string? Creator,
    string? Producer,
    bool IsEncrypted,
    long FileSize);

/// <summary>Un résultat de recherche : numéro de page (à partir de 0) et extrait du texte.</summary>
public sealed record PdfSearchHit(int PageIndex, string Excerpt)
{
    public override string ToString() => $"Page {PageIndex + 1} — {Excerpt}";
}

/// <summary>
/// Décode le contenu d'un PDF avec la bibliothèque PdfPig :
/// texte des pages, recherche, informations du document.
/// </summary>
public static class PdfTextReader
{
    private const int MaxHits = 300;

    /// <summary>Texte d'une page, dans l'ordre de lecture.</summary>
    public static Task<string> GetPageTextAsync(string path, string? password, int pageIndex) =>
        Task.Run(() =>
        {
            using PdfDocument document = Open(path, password);
            Page page = document.GetPage(pageIndex + 1); // PdfPig compte les pages à partir de 1
            return ContentOrderTextExtractor.GetText(page);
        });

    /// <summary>Cherche un texte dans toutes les pages (sans tenir compte des majuscules).</summary>
    public static Task<IReadOnlyList<PdfSearchHit>> SearchAsync(
        string path, string? password, string query, CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<PdfSearchHit>>(() =>
        {
            var hits = new List<PdfSearchHit>();
            using PdfDocument document = Open(path, password);

            foreach (Page page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                string text = ContentOrderTextExtractor.GetText(page).Replace('\r', ' ').Replace('\n', ' ');
                int index = text.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);

                while (index >= 0 && hits.Count < MaxHits)
                {
                    hits.Add(new PdfSearchHit(page.Number - 1, Excerpt(text, index, query.Length)));
                    index = text.IndexOf(query, index + query.Length, StringComparison.CurrentCultureIgnoreCase);
                }

                if (hits.Count >= MaxHits)
                    break;
            }

            return hits;
        }, cancellationToken);

    /// <summary>Informations générales : pages, version, titre, auteur, logiciel de création...</summary>
    public static Task<PdfInfo> GetInfoAsync(string path, string? password) =>
        Task.Run(() =>
        {
            using PdfDocument document = Open(path, password);

            return new PdfInfo(
                document.NumberOfPages,
                document.Version.ToString(CultureInfo.InvariantCulture),
                Clean(document.Information.Title),
                Clean(document.Information.Author),
                Clean(document.Information.Creator),
                Clean(document.Information.Producer),
                document.IsEncrypted,
                new FileInfo(path).Length);
        });

    private static PdfDocument Open(string path, string? password)
    {
        if (string.IsNullOrEmpty(password))
            return PdfDocument.Open(path);

        return PdfDocument.Open(path, new ParsingOptions { Passwords = new List<string> { password } });
    }

    private static string Excerpt(string text, int index, int length)
    {
        int start = Math.Max(0, index - 35);
        int end = Math.Min(text.Length, index + length + 35);
        string excerpt = text[start..end].Trim();

        return (start > 0 ? "…" : "") + excerpt + (end < text.Length ? "…" : "");
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
