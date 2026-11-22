namespace KEYCE_KODER.Core.Models;

public sealed record SearchOptions(bool MatchCase = false, bool WholeWord = false);

public readonly record struct SearchMatch(int Index, int Length);

public readonly record struct ReplaceAllResult(string Text, int Count);