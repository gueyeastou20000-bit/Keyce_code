using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace KEYCE_KODER.Core.Git;

/// <summary>
/// Lance les commandes git (le programme git.exe installé sur le PC) et renvoie leurs résultats.
/// Aucune dépendance à l'interface : utilisable et testable partout.
/// </summary>
public sealed class GitService
{
    // Options ajoutées à chaque commande :
    // - core.quotepath=false : accents affichés normalement (pas « \303\251 »)
    // - color.ui=never       : pas de codes couleur dans la sortie
    private static readonly string[] CommonOptions =
    [
        "-c", "core.quotepath=false",
        "-c", "color.ui=never"
    ];

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    // ---------------- Exécution ----------------

    /// <summary>Lance « git arguments... » dans le dossier indiqué.</summary>
    public async Task<GitResult> RunAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(workingDirectory))
            return GitResult.Fail($"Le dossier n'existe pas : {workingDirectory}");

        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Utf8,
            StandardErrorEncoding = Utf8
        };

        foreach (string option in CommonOptions)
            startInfo.ArgumentList.Add(option);

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        // Jamais de question dans une console invisible (sinon git attendrait indéfiniment)
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        // « git status » ne réécrit pas l'index (évite des rafraîchissements en boucle)
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        // Les chemins sont pris à la lettre (un fichier nommé « *.txt » reste « *.txt »)
        startInfo.Environment["GIT_LITERAL_PATHSPECS"] = "1";

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            return new GitResult(-1, string.Empty, "Git est introuvable sur ce PC.");
        }

        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* déjà terminé */ }
            throw;
        }

        return new GitResult(process.ExitCode, await output, await error);
    }

    /// <summary>Git est-il installé (commande « git » disponible) ?</summary>
    public async Task<bool> IsInstalledAsync()
    {
        GitResult result = await RunAsync(Path.GetTempPath(), ["--version"]);
        return result.Success;
    }

    // ---------------- Dépôt ----------------

    /// <summary>Racine du dépôt qui contient ce dossier, ou null s'il n'est pas dans un dépôt Git.</summary>
    public async Task<string?> FindRepositoryRootAsync(string folder)
    {
        GitResult result = await RunAsync(folder, ["rev-parse", "--show-toplevel"]);
        if (!result.Success)
            return null;

        string root = result.Output.Trim();
        return root.Length == 0 ? null : Path.GetFullPath(root);
    }

    /// <summary>Crée un nouveau dépôt (branche « main ») dans ce dossier.</summary>
    public Task<GitResult> InitAsync(string folder) =>
        RunAsync(folder, ["init", "-b", "main"]);

    /// <summary>Copie un dépôt distant (GitHub...) dans le dossier destination (qui ne doit pas exister ou être vide).</summary>
    public Task<GitResult> CloneAsync(string url, string destination)
    {
        string parent = Path.GetDirectoryName(Path.GetFullPath(destination)) ?? Path.GetTempPath();
        return RunAsync(parent, ["clone", "--", url, destination]);
    }

    /// <summary>Le dépôt a-t-il au moins un commit ?</summary>
    public async Task<bool> HasCommitsAsync(string root)
    {
        GitResult result = await RunAsync(root, ["rev-parse", "--verify", "-q", "HEAD"]);
        return result.Success;
    }

    // ---------------- État ----------------

    /// <summary>Branche et fichiers modifiés.</summary>
    public async Task<GitStatus> GetStatusAsync(string root)
    {
        GitResult result = await RunAsync(root, ["status", "--porcelain=v1", "-b", "-z", "--untracked-files=all"]);
        if (!result.Success)
            throw new GitException(result.Message);

        return GitStatusParser.Parse(result.Output);
    }

    /// <summary>Derniers commits (vide si le dépôt n'a encore aucun commit).</summary>
    public async Task<IReadOnlyList<GitCommitInfo>> GetHistoryAsync(string root, int count = 20)
    {
        GitResult result = await RunAsync(root, ["log", $"-{count}", "--pretty=format:%h%x1f%an%x1f%ar%x1f%s"]);
        return result.Success ? GitStatusParser.ParseLog(result.Output) : [];
    }

    /// <summary>Différences d'un fichier (format « diff » : lignes + ajoutées, - supprimées).</summary>
    public async Task<string> GetDiffAsync(string root, GitFileChange change)
    {
        if (change.Kind == GitChangeKind.Untracked)
            return await DescribeNewFileAsync(root, change);

        // Indexé : différence index ↔ dernier commit ; non indexé : fichier ↔ index
        var arguments = new List<string> { "diff" };

        if (change.Staged)
        {
            arguments.Add("--cached");
            arguments.Add("-M");
        }

        arguments.Add("--");

        if (change.Staged && change.OriginalPath is not null)
            arguments.Add(change.OriginalPath);

        arguments.Add(change.Path);

        GitResult result = await RunAsync(root, arguments);
        return result.Success ? result.Output : result.Message;
    }

    // ---------------- Index (indexer / désindexer / annuler) ----------------

    /// <summary>Indexe des fichiers (« git add »), y compris les suppressions.</summary>
    public Task<GitResult> StageAsync(string root, IEnumerable<string> paths) =>
        RunAsync(root, ["add", "-A", "--", .. paths]);

    /// <summary>Indexe toutes les modifications du dépôt.</summary>
    public Task<GitResult> StageAllAsync(string root) =>
        RunAsync(root, ["add", "-A"]);

    /// <summary>Retire des fichiers de l'index (les modifications restent dans les fichiers).</summary>
    public async Task<GitResult> UnstageAsync(string root, IEnumerable<string> paths)
    {
        string[] list = paths.ToArray();

        // Sans aucun commit, il n'y a pas de « HEAD » à restaurer : on retire simplement de l'index
        if (await HasCommitsAsync(root))
            return await RunAsync(root, ["restore", "--staged", "--", .. list]);

        return await RunAsync(root, ["rm", "--cached", "-r", "-q", "--", .. list]);
    }

    /// <summary>Retire tous les fichiers de l'index.</summary>
    public Task<GitResult> UnstageAllAsync(string root) =>
        UnstageAsync(root, ["."]);

    /// <summary>
    /// Annule les modifications non indexées d'un fichier (définitif).
    /// Un fichier non suivi (nouveau) est supprimé.
    /// </summary>
    public async Task<GitResult> DiscardAsync(string root, GitFileChange change)
    {
        if (change.Kind != GitChangeKind.Untracked)
            return await RunAsync(root, ["restore", "--", change.Path]);

        string fullPath = ToFullPath(root, change.Path);

        try
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            else if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);

            return GitResult.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return GitResult.Fail(ex.Message);
        }
    }

    // ---------------- Commit ----------------

    /// <summary>Valide les modifications indexées avec ce message.</summary>
    public Task<GitResult> CommitAsync(string root, string message) =>
        RunAsync(root, ["commit", "-m", message]);

    /// <summary>Nom et e-mail configurés ? (obligatoires pour faire un commit)</summary>
    public async Task<bool> HasIdentityAsync(string root)
    {
        GitResult name = await RunAsync(root, ["config", "user.name"]);
        GitResult email = await RunAsync(root, ["config", "user.email"]);

        return name.Success && email.Success
               && name.Output.Trim().Length > 0
               && email.Output.Trim().Length > 0;
    }

    /// <summary>Enregistre le nom et l'e-mail pour tous les dépôts de ce PC (« git config --global »).</summary>
    public async Task<GitResult> SetIdentityAsync(string root, string name, string email)
    {
        GitResult result = await RunAsync(root, ["config", "--global", "user.name", name]);
        if (!result.Success)
            return result;

        return await RunAsync(root, ["config", "--global", "user.email", email]);
    }

    // ---------------- Branches ----------------

    /// <summary>Branches locales, et branches distantes qui n'ont pas encore de copie locale.</summary>
    public async Task<GitBranches> GetBranchesAsync(string root)
    {
        GitResult local = await RunAsync(root, ["for-each-ref", "--format=%(refname:short)", "refs/heads"]);
        GitResult remote = await RunAsync(root, ["for-each-ref", "--format=%(refname:short)", "refs/remotes"]);

        List<string> locals = Lines(local.Output);

        // « origin/feature » → proposé seulement s'il n'existe pas déjà une branche locale « feature »
        List<string> remotes = Lines(remote.Output)
            .Where(name => name.Contains('/') && !name.EndsWith("/HEAD", StringComparison.Ordinal))
            .Where(name => !locals.Contains(name[(name.IndexOf('/') + 1)..]))
            .ToList();

        return new GitBranches(locals, remotes);
    }

    /// <summary>Change de branche.</summary>
    public Task<GitResult> SwitchAsync(string root, string branch) =>
        RunAsync(root, ["switch", branch]);

    /// <summary>Crée une branche locale à partir d'une branche distante (ex. « origin/feature ») et s'y place.</summary>
    public Task<GitResult> SwitchToRemoteAsync(string root, string remoteBranch) =>
        RunAsync(root, ["switch", "--track", remoteBranch]);

    /// <summary>Crée une nouvelle branche et s'y place.</summary>
    public Task<GitResult> CreateBranchAsync(string root, string name) =>
        RunAsync(root, ["switch", "-c", name]);

    // ---------------- Dépôt distant (GitHub...) ----------------

    /// <summary>Noms des dépôts distants (ex. « origin »).</summary>
    public async Task<IReadOnlyList<string>> GetRemotesAsync(string root)
    {
        GitResult result = await RunAsync(root, ["remote"]);
        return Lines(result.Output);
    }

    public Task<GitResult> AddRemoteAsync(string root, string name, string url) =>
        RunAsync(root, ["remote", "add", name, url]);

    /// <summary>Récupère les nouveautés de tous les dépôts distants (sans modifier les fichiers).</summary>
    public Task<GitResult> FetchAsync(string root) =>
        RunAsync(root, ["fetch", "--all", "--prune"]);

    /// <summary>Récupère et fusionne les commits de la branche distante suivie.</summary>
    public Task<GitResult> PullAsync(string root) =>
        RunAsync(root, ["pull", "--no-rebase", "--no-edit"]);

    /// <summary>
    /// Envoie les commits. remote = null : la branche est déjà liée à une branche distante.
    /// Sinon, première publication : « git push -u remote branche ».
    /// </summary>
    public Task<GitResult> PushAsync(string root, string branch, string? remote)
    {
        return remote is null
            ? RunAsync(root, ["push"])
            : RunAsync(root, ["push", "-u", remote, branch]);
    }

    // ---------------- Outils ----------------

    /// <summary>Chemin complet d'un fichier du dépôt (chemin Git « src/main.py » → « C:\...\src\main.py »).</summary>
    public static string ToFullPath(string root, string gitPath) =>
        Path.GetFullPath(Path.Combine(root, gitPath.Replace('/', Path.DirectorySeparatorChar)));

    private static List<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    /// <summary>Contenu d'un nouveau fichier présenté comme un diff (toutes les lignes ajoutées).</summary>
    private static async Task<string> DescribeNewFileAsync(string root, GitFileChange change)
    {
        string fullPath = ToFullPath(root, change.Path);

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
                return "(Fichier introuvable.)";

            if (info.Length > 1_000_000)
                return "(Fichier trop volumineux pour être affiché.)";

            string text = await File.ReadAllTextAsync(fullPath);
            if (text.Contains('\0'))
                return "(Fichier binaire : aperçu impossible.)";

            var builder = new StringBuilder();
            builder.Append("--- /dev/null\n");
            builder.Append("+++ b/").Append(change.Path).Append('\n');

            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
                builder.Append('+').Append(line).Append('\n');

            return builder.ToString();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"(Lecture impossible : {ex.Message})";
        }
    }
}
