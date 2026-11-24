using KEYCE_KODER.Core.Git;

namespace KEYCE_KODER.Tests;

/// <summary>
/// Tests avec le vrai programme git (ignorés si Git n'est pas installé).
/// Chaque test travaille dans un dossier temporaire, supprimé à la fin.
/// </summary>
[TestClass]
public sealed class GitServiceTests
{
    private readonly GitService _git = new();
    private string _root = string.Empty;

    [TestInitialize]
    public async Task CreerDepotDeTest()
    {
        if (!await _git.IsInstalledAsync())
            Assert.Inconclusive("Git n'est pas installé sur ce PC.");

        _root = Path.Combine(Path.GetTempPath(), "keyce_git_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        Assert.IsTrue((await _git.InitAsync(_root)).Success);

        // Identité locale au dépôt de test (ne touche pas à la configuration du PC)
        await _git.RunAsync(_root, ["config", "user.name", "Test KEYCE"]);
        await _git.RunAsync(_root, ["config", "user.email", "test@keyce.cm"]);
    }

    [TestCleanup]
    public void SupprimerDepotDeTest()
    {
        if (!Directory.Exists(_root))
            return;

        // Git met certains fichiers en lecture seule : on enlève cet attribut avant de supprimer
        foreach (string file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task NouveauDepot_FichierNonSuivi_PuisIndexe_PuisCommit()
    {
        File.WriteAllText(Path.Combine(_root, "main.py"), "print('bonjour')\n");

        GitStatus status = await _git.GetStatusAsync(_root);
        Assert.AreEqual("main", status.Branch);
        Assert.IsFalse(status.HasCommits);
        Assert.AreEqual(GitChangeKind.Untracked, status.Changes.Single().Kind);

        Assert.IsTrue((await _git.StageAllAsync(_root)).Success);
        status = await _git.GetStatusAsync(_root);
        Assert.AreEqual(GitChangeKind.Added, status.Staged.Single().Kind);

        GitResult commit = await _git.CommitAsync(_root, "Premier commit");
        Assert.IsTrue(commit.Success, commit.Message);

        status = await _git.GetStatusAsync(_root);
        Assert.IsTrue(status.IsClean);
        Assert.IsTrue(status.HasCommits);

        IReadOnlyList<GitCommitInfo> history = await _git.GetHistoryAsync(_root);
        Assert.AreEqual("Premier commit", history.Single().Subject);
    }

    [TestMethod]
    public async Task Desindexer_SansCommit()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        await _git.StageAllAsync(_root);

        Assert.IsTrue((await _git.UnstageAllAsync(_root)).Success);

        GitStatus status = await _git.GetStatusAsync(_root);
        Assert.IsEmpty(status.Staged);
        Assert.AreEqual(GitChangeKind.Untracked, status.Changes.Single().Kind);
    }

    [TestMethod]
    public async Task AnnulerModification_RestaureLeFichier()
    {
        string file = Path.Combine(_root, "a.txt");
        File.WriteAllText(file, "version 1");
        await _git.StageAllAsync(_root);
        await _git.CommitAsync(_root, "v1");

        File.WriteAllText(file, "version 2");
        GitFileChange change = (await _git.GetStatusAsync(_root)).Changes.Single();
        Assert.AreEqual(GitChangeKind.Modified, change.Kind);

        string diff = await _git.GetDiffAsync(_root, change);
        Assert.IsTrue(diff.Contains("+version 2"), diff);

        Assert.IsTrue((await _git.DiscardAsync(_root, change)).Success);
        Assert.AreEqual("version 1", File.ReadAllText(file));
    }

    [TestMethod]
    public async Task NouvelleBranche_PuisRetourSurMain()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
        await _git.StageAllAsync(_root);
        await _git.CommitAsync(_root, "init");

        Assert.IsTrue((await _git.CreateBranchAsync(_root, "feature/test")).Success);
        Assert.AreEqual("feature/test", (await _git.GetStatusAsync(_root)).Branch);

        GitBranches branches = await _git.GetBranchesAsync(_root);
        Assert.IsTrue(branches.Local.Contains("main"));
        Assert.IsTrue(branches.Local.Contains("feature/test"));

        Assert.IsTrue((await _git.SwitchAsync(_root, "main")).Success);
        Assert.AreEqual("main", (await _git.GetStatusAsync(_root)).Branch);
    }
}
