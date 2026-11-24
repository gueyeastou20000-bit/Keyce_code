using System.IO;
using KEYCE_KODER.Runner.Debugging;

namespace KEYCE_KODER.App.Debugging;

/// <summary>Un point d'arrêt posé dans un fichier.</summary>
public sealed class Breakpoint
{
    public Breakpoint(string filePath, int line)
    {
        FilePath = filePath;
        Line = line;
    }

    public string FilePath { get; }

    /// <summary>Numéro de ligne (à partir de 1). Suit le texte quand on ajoute ou supprime des lignes.</summary>
    public int Line { get; set; }

    /// <summary>Condition Python facultative (ex. « i == 5 ») : on ne s'arrête que si elle est vraie.</summary>
    public string? Condition { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>false si Python a refusé la ligne (ligne vide, hors du fichier...).</summary>
    public bool Verified { get; set; } = true;

    public override string ToString() =>
        $"{Path.GetFileName(FilePath)} : {Line}" + (string.IsNullOrWhiteSpace(Condition) ? "" : $"   si {Condition}");
}

/// <summary>Tous les points d'arrêt, rangés par fichier. Partagé par les éditeurs, la vue Débogage et la session.</summary>
public sealed class BreakpointStore
{
    private readonly Dictionary<string, List<Breakpoint>> _byFile = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Les points d'arrêt d'un fichier ont changé (paramètre : chemin complet du fichier).</summary>
    public event Action<string>? Changed;

    public IEnumerable<Breakpoint> All =>
        _byFile.Values.SelectMany(list => list).OrderBy(b => b.FilePath).ThenBy(b => b.Line);

    public IReadOnlyList<Breakpoint> For(string? filePath)
    {
        if (filePath is null)
            return [];

        return _byFile.TryGetValue(Normalize(filePath), out List<Breakpoint>? list) ? list : [];
    }

    public Breakpoint? Find(string filePath, int line) =>
        For(filePath).FirstOrDefault(b => b.Line == line);

    /// <summary>Ajoute un point d'arrêt sur la ligne, ou le retire s'il existe déjà (comme F9 dans VS Code).</summary>
    public void Toggle(string filePath, int line)
    {
        string file = Normalize(filePath);
        Breakpoint? existing = Find(file, line);

        if (existing is not null)
        {
            Remove(existing);
            return;
        }

        if (!_byFile.TryGetValue(file, out List<Breakpoint>? list))
            _byFile[file] = list = new List<Breakpoint>();

        list.Add(new Breakpoint(file, line));
        Changed?.Invoke(file);
    }

    public void Remove(Breakpoint breakpoint)
    {
        if (_byFile.TryGetValue(breakpoint.FilePath, out List<Breakpoint>? list) && list.Remove(breakpoint))
        {
            if (list.Count == 0)
                _byFile.Remove(breakpoint.FilePath);

            Changed?.Invoke(breakpoint.FilePath);
        }
    }

    public void RemoveAll()
    {
        List<string> files = _byFile.Keys.ToList();
        _byFile.Clear();

        foreach (string file in files)
            Changed?.Invoke(file);
    }

    public void SetEnabled(Breakpoint breakpoint, bool enabled)
    {
        breakpoint.Enabled = enabled;
        Changed?.Invoke(breakpoint.FilePath);
    }

    public void SetCondition(Breakpoint breakpoint, string? condition)
    {
        breakpoint.Condition = string.IsNullOrWhiteSpace(condition) ? null : condition.Trim();
        Changed?.Invoke(breakpoint.FilePath);
    }

    /// <summary>
    /// Les lignes ont bougé (texte modifié) : on retire les doublons et on prévient.
    /// Appelé par la marge de l'éditeur.
    /// </summary>
    public void NotifyMoved(string filePath)
    {
        string file = Normalize(filePath);
        if (!_byFile.TryGetValue(file, out List<Breakpoint>? list))
            return;

        List<Breakpoint> unique = list.GroupBy(b => b.Line).Select(g => g.First()).ToList();
        list.Clear();
        list.AddRange(unique);

        Changed?.Invoke(file);
    }

    /// <summary>Points d'arrêt actifs d'un fichier, au format attendu par la session de débogage.</summary>
    public IReadOnlyList<SourceBreakpoint> ToSource(string filePath) =>
        For(filePath)
            .Where(b => b.Enabled)
            .Select(b => new SourceBreakpoint(b.Line, b.Condition))
            .ToList();

    /// <summary>Tous les points d'arrêt actifs, par fichier.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<SourceBreakpoint>> ToSourceByFile() =>
        _byFile.Keys.ToDictionary(file => file, file => ToSource(file), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string filePath) => Path.GetFullPath(filePath);
}
