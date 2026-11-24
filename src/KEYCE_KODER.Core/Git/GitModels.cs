namespace KEYCE_KODER.Core.Git;

/// <summary>Type de modification d'un fichier, tel que Git le rapporte.</summary>
public enum GitChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
    Untracked,
    Conflicted
}

/// <summary>
/// Un fichier modifié dans le dépôt.
/// Path : chemin relatif à la racine du dépôt, avec des « / » (ex. « src/main.py »).
/// Staged : true si la modification est indexée (prête à être validée par un commit).
/// OriginalPath : ancien chemin pour un fichier renommé ou copié.
/// </summary>
public sealed record GitFileChange(string Path, GitChangeKind Kind, bool Staged, string? OriginalPath = null)
{
    /// <summary>Nom du fichier seul (ex. « main.py »).</summary>
    public string FileName
    {
        get
        {
            string trimmed = Path.TrimEnd('/');
            int slash = trimmed.LastIndexOf('/');
            return slash < 0 ? trimmed : trimmed[(slash + 1)..];
        }
    }

    /// <summary>Dossier relatif du fichier (ex. « src »), vide s'il est à la racine.</summary>
    public string Folder
    {
        get
        {
            string trimmed = Path.TrimEnd('/');
            int slash = trimmed.LastIndexOf('/');
            return slash < 0 ? string.Empty : trimmed[..slash];
        }
    }

    /// <summary>Lettre affichée devant le fichier, comme dans VS Code.</summary>
    public string Letter => Kind switch
    {
        GitChangeKind.Modified => "M",
        GitChangeKind.Added => "A",
        GitChangeKind.Deleted => "D",
        GitChangeKind.Renamed => "R",
        GitChangeKind.Copied => "C",
        GitChangeKind.TypeChanged => "T",
        GitChangeKind.Untracked => "U",
        GitChangeKind.Conflicted => "!",
        _ => "?"
    };

    /// <summary>Description en français du type de modification.</summary>
    public string Description => Kind switch
    {
        GitChangeKind.Modified => "Modifié",
        GitChangeKind.Added => "Ajouté",
        GitChangeKind.Deleted => "Supprimé",
        GitChangeKind.Renamed => "Renommé",
        GitChangeKind.Copied => "Copié",
        GitChangeKind.TypeChanged => "Type modifié",
        GitChangeKind.Untracked => "Non suivi (nouveau fichier)",
        GitChangeKind.Conflicted => "Conflit de fusion",
        _ => "Inconnu"
    };
}

/// <summary>État du dépôt : branche, avance/retard, fichiers modifiés.</summary>
public sealed record GitStatus(
    string? Branch,
    string? Upstream,
    int Ahead,
    int Behind,
    bool UpstreamGone,
    bool HasCommits,
    IReadOnlyList<GitFileChange> Staged,
    IReadOnlyList<GitFileChange> Changes,
    IReadOnlyList<GitFileChange> Conflicts)
{
    /// <summary>HEAD détaché : aucune branche active.</summary>
    public bool IsDetached => Branch is null;

    public bool HasStaged => Staged.Count > 0;

    public bool HasChanges => Changes.Count > 0;

    public bool HasConflicts => Conflicts.Count > 0;

    public bool IsClean => !HasStaged && !HasChanges && !HasConflicts;
}

/// <summary>Un commit de l'historique.</summary>
public sealed record GitCommitInfo(string Hash, string Author, string When, string Subject);

/// <summary>Branches locales et branches distantes (sans copie locale).</summary>
public sealed record GitBranches(IReadOnlyList<string> Local, IReadOnlyList<string> Remote);

/// <summary>Résultat d'une commande git : code de sortie et textes affichés.</summary>
public sealed record GitResult(int ExitCode, string Output, string Error)
{
    public bool Success => ExitCode == 0;

    /// <summary>Texte à montrer à l'utilisateur (erreur en priorité).</summary>
    public string Message
    {
        get
        {
            string text = string.IsNullOrWhiteSpace(Error) ? Output : Error;
            return text.Trim();
        }
    }

    public static GitResult Ok() => new(0, string.Empty, string.Empty);

    public static GitResult Fail(string message) => new(1, string.Empty, message);
}

/// <summary>Erreur renvoyée par Git (ex. dépôt corrompu).</summary>
public sealed class GitException(string message) : Exception(message);
