namespace KEYCE_KODER.Core.Git;

/// <summary>
/// Lit la sortie des commandes Git (sans interface : testable).
/// Statut : « git status --porcelain=v1 -b -z » (entrées séparées par le caractère NUL).
/// Historique : « git log --pretty=format:%h%x1f%an%x1f%ar%x1f%s ».
/// </summary>
public static class GitStatusParser
{
    /// <summary>Séparateur de champs utilisé pour l'historique (%x1f).</summary>
    public const char FieldSeparator = '\u001f';

    public static GitStatus Parse(string porcelain)
    {
        var branch = new BranchInfo();
        var staged = new List<GitFileChange>();
        var changes = new List<GitFileChange>();
        var conflicts = new List<GitFileChange>();

        string[] entries = porcelain.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < entries.Length; i++)
        {
            string entry = entries[i];

            // Première ligne : la branche (ex. « ## main...origin/main [ahead 1] »)
            if (entry.StartsWith("## ", StringComparison.Ordinal))
            {
                branch = ParseBranch(entry[3..]);
                continue;
            }

            if (entry.Length < 4)
                continue;

            // « XY chemin » : X = index (indexé), Y = dossier de travail (non indexé)
            char x = entry[0];
            char y = entry[1];
            string path = entry[3..];

            // Renommage / copie : l'ancien chemin suit dans l'entrée suivante
            string? original = null;
            if ((x is 'R' or 'C' || y is 'R' or 'C') && i + 1 < entries.Length)
                original = entries[++i];

            if (x == '?' && y == '?')
            {
                changes.Add(new GitFileChange(path, GitChangeKind.Untracked, Staged: false));
                continue;
            }

            if (x == '!')
                continue; // fichier ignoré

            if (IsConflict(x, y))
            {
                conflicts.Add(new GitFileChange(path, GitChangeKind.Conflicted, Staged: false));
                continue;
            }

            if (x != ' ')
                staged.Add(new GitFileChange(path, KindOf(x), Staged: true, (x is 'R' or 'C') ? original : null));

            if (y != ' ')
                changes.Add(new GitFileChange(path, KindOf(y), Staged: false, (y is 'R' or 'C') ? original : null));
        }

        return new GitStatus(
            branch.Name,
            branch.Upstream,
            branch.Ahead,
            branch.Behind,
            branch.Gone,
            branch.HasCommits,
            Sorted(staged),
            Sorted(changes),
            Sorted(conflicts));
    }

    /// <summary>Lit l'historique : une ligne par commit, champs séparés par %x1f.</summary>
    public static IReadOnlyList<GitCommitInfo> ParseLog(string log)
    {
        var commits = new List<GitCommitInfo>();

        foreach (string rawLine in log.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            string[] fields = line.Split(FieldSeparator);

            if (fields.Length < 4)
                continue;

            // Le sujet peut lui-même contenir le séparateur : on recolle la fin
            string subject = string.Join(FieldSeparator, fields[3..]);
            commits.Add(new GitCommitInfo(fields[0], fields[1], fields[2], subject));
        }

        return commits;
    }

    // ---------------- Branche ----------------

    private sealed class BranchInfo
    {
        public string? Name { get; set; }
        public string? Upstream { get; set; }
        public int Ahead { get; set; }
        public int Behind { get; set; }
        public bool Gone { get; set; }
        public bool HasCommits { get; set; } = true;
    }

    private static BranchInfo ParseBranch(string header)
    {
        var info = new BranchInfo();

        // Dépôt sans commit : « No commits yet on main » (ou « Initial commit on main » avec un vieux Git)
        foreach (string prefix in new[] { "No commits yet on ", "Initial commit on " })
        {
            if (header.StartsWith(prefix, StringComparison.Ordinal))
            {
                info.HasCommits = false;
                header = header[prefix.Length..];
                break;
            }
        }

        // HEAD détaché : pas de branche active
        if (header.StartsWith("HEAD (no branch)", StringComparison.Ordinal))
            return info;

        // Partie entre crochets : « [ahead 2, behind 1] » ou « [gone] »
        int bracket = header.IndexOf(" [", StringComparison.Ordinal);
        if (bracket >= 0)
        {
            string details = header[(bracket + 2)..].TrimEnd(']');
            header = header[..bracket];

            foreach (string part in details.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.StartsWith("ahead ", StringComparison.Ordinal) && int.TryParse(part[6..], out int ahead))
                    info.Ahead = ahead;
                else if (part.StartsWith("behind ", StringComparison.Ordinal) && int.TryParse(part[7..], out int behind))
                    info.Behind = behind;
                else if (part == "gone")
                    info.Gone = true;
            }
        }

        // « main...origin/main » : branche locale ... branche distante suivie
        int dots = header.IndexOf("...", StringComparison.Ordinal);
        if (dots >= 0)
        {
            info.Name = header[..dots];
            info.Upstream = header[(dots + 3)..];
        }
        else
        {
            info.Name = header;
        }

        return info;
    }

    // ---------------- Fichiers ----------------

    private static bool IsConflict(char x, char y) =>
        x == 'U' || y == 'U' || (x == 'A' && y == 'A') || (x == 'D' && y == 'D');

    private static GitChangeKind KindOf(char code) => code switch
    {
        'M' => GitChangeKind.Modified,
        'A' => GitChangeKind.Added,
        'D' => GitChangeKind.Deleted,
        'R' => GitChangeKind.Renamed,
        'C' => GitChangeKind.Copied,
        'T' => GitChangeKind.TypeChanged,
        _ => GitChangeKind.Modified
    };

    private static List<GitFileChange> Sorted(List<GitFileChange> changes) =>
        changes.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList();
}
