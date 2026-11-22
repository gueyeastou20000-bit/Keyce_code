using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class SearchServiceTests
{
    private readonly SearchService _search = new();
    private readonly SearchOptions _defaut = new();

    [TestMethod]
    public void FindNext_TrouveLaPremiereOccurrence()
    {
        SearchMatch? match = _search.FindNext("abc abc", "abc", 0, _defaut);

        Assert.IsNotNull(match);
        Assert.AreEqual(0, match.Value.Index);
        Assert.AreEqual(3, match.Value.Length);
    }

    [TestMethod]
    public void FindNext_PartDeLaPositionDonnee()
    {
        SearchMatch? match = _search.FindNext("abc abc", "abc", 1, _defaut);

        Assert.IsNotNull(match);
        Assert.AreEqual(4, match.Value.Index);
    }

    [TestMethod]
    public void FindNext_RepartDuDebutSiRienApres()
    {
        SearchMatch? match = _search.FindNext("abc abc", "abc", 5, _defaut);

        Assert.IsNotNull(match);
        Assert.AreEqual(0, match.Value.Index);
    }

    [TestMethod]
    public void FindNext_RetourneNullSiIntrouvable()
    {
        Assert.IsNull(_search.FindNext("abc", "xyz", 0, _defaut));
    }

    [TestMethod]
    public void FindNext_RespecteLaCasse()
    {
        SearchMatch? match = _search.FindNext(
            "Abc abc", "abc", 0, new SearchOptions(MatchCase: true));

        Assert.IsNotNull(match);
        Assert.AreEqual(4, match.Value.Index);
    }

    [TestMethod]
    public void FindNext_IgnoreLaCasseParDefaut()
    {
        SearchMatch? match = _search.FindNext("Abc", "abc", 0, _defaut);

        Assert.IsNotNull(match);
        Assert.AreEqual(0, match.Value.Index);
    }

    [TestMethod]
    public void FindNext_MotEntier()
    {
        SearchMatch? match = _search.FindNext(
            "printer print", "print", 0, new SearchOptions(WholeWord: true));

        Assert.IsNotNull(match);
        Assert.AreEqual(8, match.Value.Index);
    }
    [TestMethod]
    public void FindPrevious_TrouveLOccurrenceAvant()
    {
        SearchMatch? match = _search.FindPrevious("abc abc abc", "abc", 8, _defaut);

        Assert.IsNotNull(match);
        Assert.AreEqual(4, match.Value.Index);
    }

    [TestMethod]
    public void FindPrevious_RepartDeLaFin()
    {
        SearchMatch? match = _search.FindPrevious("abc abc abc", "abc", 0, _defaut);

        Assert.IsNotNull(match);
        Assert.AreEqual(8, match.Value.Index);
    }

    [TestMethod]
    public void ReplaceAll_RemplaceToutesLesOccurrences()
    {
        ReplaceAllResult result = _search.ReplaceAll("a b a b", "a", "x", _defaut);

        Assert.AreEqual("x b x b", result.Text);
        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    public void ReplaceAll_LesCaracteresSpeciauxSontLitteraux()
    {
        ReplaceAllResult result = _search.ReplaceAll("a.b acb", "a.b", "$1", _defaut);

        Assert.AreEqual("$1 acb", result.Text);
        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    public void CountMatches_CompteLesOccurrences()
    {
        Assert.AreEqual(3, _search.CountMatches("ab ab ab", "ab", _defaut));
    }

    [TestMethod]
    public void IsExactMatch_VerifieLaSelection()
    {
        Assert.IsTrue(_search.IsExactMatch("Print", "print", _defaut));
        Assert.IsFalse(_search.IsExactMatch("Print", "print", new SearchOptions(MatchCase: true)));
        Assert.IsFalse(_search.IsExactMatch("printer", "print", _defaut));
    }
}