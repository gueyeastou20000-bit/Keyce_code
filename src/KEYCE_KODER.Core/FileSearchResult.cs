namespace KEYCE_KODER.Core.Models;

/// <summary>Une occurrence trouvée : numéro de ligne, texte de la ligne, position dans le fichier.</summary>
public readonly record struct LineMatch(int LineNumber, string LineText, int Index, int Length);

/// <summary>Un fichier contenant au moins une occurrence.</summary>
public sealed record FileSearchResult(string FilePath, IReadOnlyList<LineMatch> Matches);

/// <summary>Le résultat complet d'une recherche dans un dossier.</summary>
public sealed record FileSearchReport(IReadOnlyList<FileSearchResult> Files, int TotalMatches, bool Truncated);