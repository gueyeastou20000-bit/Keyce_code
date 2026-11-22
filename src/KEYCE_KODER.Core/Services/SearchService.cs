using System.Text.RegularExpressions;
using KEYCE_KODER.Core.Models;
using Microsoft.VisualBasic.FileIO;


namespace KEYCE_KODER.Core.Services;

public class SearchService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public SearchMatch? FindNext(
        string text,
        string query,
        int startIndex,
        SearchOptions options,
        bool wrapAround = true)
    {
        if (string.IsNullOrEmpty(query))
            return null;

        Regex regex = BuildRegex(query, options);
        startIndex = Math.Clamp(startIndex, 0, text.Length);

        Match match = regex.Match(text, startIndex);

        if (!match.Success && wrapAround && startIndex > 0)
            match = regex.Match(text, 0);

        return match.Success ? new SearchMatch(match.Index, match.Length) : null;
    }

    public SearchMatch? FindPrevious(
        string text,
        string query,
        int startIndex,
        SearchOptions options,
        bool wrapAround = true)
    {
        if (string.IsNullOrEmpty(query))
            return null;

        MatchCollection matches = BuildRegex(query, options).Matches(text);

        if (matches.Count == 0)
            return null;

        SearchMatch? previous = null;

        foreach (Match match in matches)
        {
            if (match.Index >= startIndex)
                break;

            previous = new SearchMatch(match.Index, match.Length);
        }

        if (previous is null && wrapAround)
        {
            Match last = matches[matches.Count - 1];
            previous = new SearchMatch(last.Index, last.Length);
        }

        return previous;
    }
    public int CountMatches(string text, string query, SearchOptions options)
    {
        if (string.IsNullOrEmpty(query))
            return 0;

        return BuildRegex(query, options).Matches(text).Count;
    }

    public bool IsExactMatch(string candidate, string query, SearchOptions options)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(candidate))
            return false;

        Match match = BuildRegex(query, options).Match(candidate);

        return match.Success
            && match.Index == 0
            && match.Length == candidate.Length;
    }

    public ReplaceAllResult ReplaceAll(
        string text,
        string query,
        string replacement,
        SearchOptions options)
    {
        if (string.IsNullOrEmpty(query))
            return new ReplaceAllResult(text, 0);

        int count = 0;

        string result = BuildRegex(query, options).Replace(text, _ =>
        {
            count++;
            return replacement;
        });

        return new ReplaceAllResult(result, count);
    }

    private static Regex BuildRegex(string query, SearchOptions options)
    {
        string pattern = Regex.Escape(query);

        if (options.WholeWord)
            pattern = $@"(?<!\w){pattern}(?!\w)";

        RegexOptions regexOptions = RegexOptions.CultureInvariant;

        if (!options.MatchCase)
            regexOptions |= RegexOptions.IgnoreCase;

        return new Regex(pattern, regexOptions, Timeout);
    }
}