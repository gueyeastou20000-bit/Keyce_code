using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class FileSearchServiceTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreerDossierDeTest()
    {
        _root = Path.Combine(Path.GetTempPath(), "keyce_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void SupprimerDossierDeTest()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task SearchAsync_TrouveDansPlusieursFichiersEtSousDossiers()
    {
        File.WriteAllText(Path.Combine(_root, "a.py"), "print(1)\nx = 2\nprint(3)");
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        File.WriteAllText(Path.Combine(_root, "src", "b.py"), "print(4)");

        FileSearchReport report = await new FileSearchService()
            .SearchAsync(_root, "print", new SearchOptions());

        Assert.AreEqual(3, report.TotalMatches);
        Assert.AreEqual(2, report.Files.Count);
        Assert.IsFalse(report.Truncated);
    }

    [TestMethod]
    public async Task SearchAsync_DonneLesBonsNumerosDeLigne()
    {
        File.WriteAllText(Path.Combine(_root, "a.py"), "print(1)\nx = 2\nprint(3)");

        FileSearchReport report = await new FileSearchService()
            .SearchAsync(_root, "print", new SearchOptions());

        IReadOnlyList<LineMatch> lines = report.Files[0].Matches;
        Assert.AreEqual(1, lines[0].LineNumber);
        Assert.AreEqual(3, lines[1].LineNumber);
        Assert.AreEqual("print(3)", lines[1].LineText);
    }

    [TestMethod]
    public async Task SearchAsync_IgnoreLesDossiersBinEtObj()
    {
        Directory.CreateDirectory(Path.Combine(_root, "bin"));
        File.WriteAllText(Path.Combine(_root, "bin", "c.py"), "print(5)");

        FileSearchReport report = await new FileSearchService()
            .SearchAsync(_root, "print", new SearchOptions());

        Assert.AreEqual(0, report.TotalMatches);
    }
}