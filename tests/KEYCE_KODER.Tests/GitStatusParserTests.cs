using KEYCE_KODER.Core.Git;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class GitStatusParserTests
{
    [TestMethod]
    public void Parse_DepotSansCommit_FichiersNonSuivis()
    {
        GitStatus status = GitStatusParser.Parse("## No commits yet on main\0?? a.py\0?? src/b c.py\0");

        Assert.AreEqual("main", status.Branch);
        Assert.IsFalse(status.HasCommits);
        Assert.IsNull(status.Upstream);
        Assert.IsEmpty(status.Staged);
        Assert.HasCount(2, status.Changes);
        Assert.AreEqual(GitChangeKind.Untracked, status.Changes[0].Kind);
        Assert.AreEqual("src/b c.py", status.Changes[1].Path);
    }

    [TestMethod]
    public void Parse_BrancheSuivie_AvanceEtRetard()
    {
        GitStatus status = GitStatusParser.Parse("## main...origin/main [ahead 2, behind 1]\0");

        Assert.AreEqual("main", status.Branch);
        Assert.AreEqual("origin/main", status.Upstream);
        Assert.AreEqual(2, status.Ahead);
        Assert.AreEqual(1, status.Behind);
        Assert.IsTrue(status.HasCommits);
        Assert.IsTrue(status.IsClean);
    }

    [TestMethod]
    public void Parse_BrancheDistanteSupprimee()
    {
        GitStatus status = GitStatusParser.Parse("## feature/x...origin/feature/x [gone]\0");

        Assert.AreEqual("feature/x", status.Branch);
        Assert.IsTrue(status.UpstreamGone);
    }

    [TestMethod]
    public void Parse_HeadDetache_PasDeBranche()
    {
        GitStatus status = GitStatusParser.Parse("## HEAD (no branch)\0");

        Assert.IsNull(status.Branch);
        Assert.IsTrue(status.IsDetached);
    }

    [TestMethod]
    public void Parse_IndexeEtNonIndexe_AvecRenommage()
    {
        // « RM » : renommé dans l'index ET modifié ensuite ; l'ancien nom suit l'entrée
        GitStatus status = GitStatusParser.Parse("## main\0A  d/b.txt\0RM z.txt\0a.txt\0 D old.py\0?? é.txt\0");

        Assert.HasCount(2, status.Staged);
        Assert.AreEqual(GitChangeKind.Added, status.Staged[0].Kind);
        Assert.AreEqual(GitChangeKind.Renamed, status.Staged[1].Kind);
        Assert.AreEqual("a.txt", status.Staged[1].OriginalPath);
        Assert.IsTrue(status.Staged[1].Staged);

        Assert.HasCount(3, status.Changes);
        Assert.AreEqual(GitChangeKind.Deleted, status.Changes[0].Kind);   // old.py
        Assert.AreEqual(GitChangeKind.Modified, status.Changes[1].Kind);  // z.txt
        Assert.AreEqual(GitChangeKind.Untracked, status.Changes[2].Kind); // é.txt
        Assert.IsFalse(status.Changes[1].Staged);
    }

    [TestMethod]
    public void Parse_Conflits()
    {
        GitStatus status = GitStatusParser.Parse("## main\0UU app.py\0AA new.py\0");

        Assert.HasCount(2, status.Conflicts);
        Assert.IsTrue(status.HasConflicts);
        Assert.IsEmpty(status.Changes);
        Assert.AreEqual("!", status.Conflicts[0].Letter);
    }

    [TestMethod]
    public void ParseLog_LitLesChamps()
    {
        char s = GitStatusParser.FieldSeparator;
        string log = $"0b04adc{s}Joycy{s}2 hours ago{s}Ajout du menu\n1a2b3c4{s}Ana{s}3 days ago{s}Correction";

        IReadOnlyList<GitCommitInfo> commits = GitStatusParser.ParseLog(log);

        Assert.HasCount(2, commits);
        Assert.AreEqual("0b04adc", commits[0].Hash);
        Assert.AreEqual("Joycy", commits[0].Author);
        Assert.AreEqual("Ajout du menu", commits[0].Subject);
        Assert.AreEqual("Correction", commits[1].Subject);
    }

    [TestMethod]
    public void FileChange_NomEtDossier()
    {
        var change = new GitFileChange("src/utils/math.py", GitChangeKind.Modified, Staged: false);

        Assert.AreEqual("math.py", change.FileName);
        Assert.AreEqual("src/utils", change.Folder);
        Assert.AreEqual("M", change.Letter);
    }
}
