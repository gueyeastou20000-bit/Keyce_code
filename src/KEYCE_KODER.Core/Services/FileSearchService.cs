using KEYCE_KODER.Core.Models;

namespace KEYCE_KODER.Core.Services;

public class FileSearchService
{
    public const int MaxResults = 2000;
    private const long MaxFileSize = 2 * 1024 * 1024; // 2 Mo
    private const int MaxLineLength = 200;

    private static readonly HashSet<string> IgnoredFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", "node_modules"
    };

    private readonly SearchService _search = new();

    public Task<FileSearchReport> SearchAsync(
        string rootFolder,
        string query,
        SearchOptions options,
        CancellationToken cancellationToken = default)
    {
        // Task.Run : la recherche tourne en arrière-plan, l'interface ne se fige pas
        return Task.Run(() => Search(rootFolder, query, options, cancellationToken), cancellationToken);
    }

    private FileSearchReport Search(
        string rootFolder,
        string query,
        SearchOptions options,
        CancellationToken cancellationToken)
    {
        var files = new List<FileSearchResult>();
        int total = 0;

        if (string.IsNullOrEmpty(query) || !Directory.Exists(rootFolder))
            return new FileSearchReport(files, 0, Truncated: false);

        foreach (string filePath in EnumerateFiles(rootFolder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? text = TryReadText(filePath);
            if (text is null)
                continue;

            IReadOnlyList<SearchMatch> matches = _search.FindAll(text, query, options);
            if (matches.Count == 0)
                continue;

            List<LineMatch> lines = ToLineMatches(text, matches, MaxResults - total);
            files.Add(new FileSearchResult(filePath, lines));
            total += lines.Count;

            if (total >= MaxResults)
                return new FileSearchReport(files, total, Truncated: true);
        }

        return new FileSearchReport(files, total, Truncated: false);
    }

    private static IEnumerable<string> EnumerateFiles(string rootFolder)
    {
        var pending = new Stack<string>();
        pending.Push(rootFolder);

        while (pending.Count > 0)
        {
            string folder = pending.Pop();
            string[] files;
            string[] subFolders;

            try
            {
                files = Directory.GetFiles(folder);
                subFolders = Directory.GetDirectories(folder);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue; // dossier protégé ou inaccessible : on passe
            }

            foreach (string file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                yield return file;

            foreach (string subFolder in subFolders.OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase))
            {
                if (!IgnoredFolders.Contains(Path.GetFileName(subFolder)))
                    pending.Push(subFolder);
            }
        }
    }

    private static string? TryReadText(string filePath)
    {
        try
        {
            if (new FileInfo(filePath).Length > MaxFileSize)
                return null;

            string text = File.ReadAllText(filePath);

            // Un caractère nul indique un fichier binaire (image, .exe, .dll...)
            return text.Contains('\0') ? null : text;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static List<LineMatch> ToLineMatches(string text, IReadOnlyList<SearchMatch> matches, int max)
    {
        var lines = new List<LineMatch>();
        int lineNumber = 1;
        int lineStart = 0;
        int scanned = 0;

        foreach (SearchMatch match in matches)
        {
            if (lines.Count >= max)
                break;

            // On avance jusqu'à l'occurrence en comptant les retours à la ligne
            for (; scanned < match.Index; scanned++)
            {
                if (text[scanned] == '\n')
                {
                    lineNumber++;
                    lineStart = scanned + 1;
                }
            }

            int lineEnd = text.IndexOf('\n', match.Index);
            if (lineEnd < 0)
                lineEnd = text.Length;

            string lineText = text[lineStart..lineEnd].TrimEnd('\r').Trim();
            if (lineText.Length > MaxLineLength)
                lineText = lineText[..MaxLineLength] + "…";

            lines.Add(new LineMatch(lineNumber, lineText, match.Index, match.Length));
        }

        return lines;
    }
}