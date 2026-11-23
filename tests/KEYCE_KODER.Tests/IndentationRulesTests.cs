using KEYCE_KODER.Core.Editing;
using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class IndentationRulesTests
{
    private const string Unit = "    ";
    private static readonly LanguageDefinition Python = LanguageCatalog.FromPath("a.py");
    private static readonly LanguageDefinition Js = LanguageCatalog.FromPath("a.js");

    [TestMethod]
    public void IndentAfter_GardeLIndentation()
    {
        Assert.AreEqual("    ", IndentationRules.IndentAfter("    x = 1", Python, Unit));
    }

    [TestMethod]
    public void IndentAfter_DeuxPointsPython_AjouteUnNiveau()
    {
        Assert.AreEqual("    ", IndentationRules.IndentAfter("if x:", Python, Unit));
        Assert.AreEqual("    ", IndentationRules.IndentAfter("if x:  # test", Python, Unit));
    }

    [TestMethod]
    public void IndentAfter_AccoladeJs_AjouteUnNiveau()
    {
        Assert.AreEqual("        ", IndentationRules.IndentAfter("    function f() {", Js, Unit));
    }

    [TestMethod]
    public void IndentAfter_DeuxPointsJs_NAjoutePasDeNiveau()
    {
        Assert.AreEqual("", IndentationRules.IndentAfter("case 1:", Js, Unit));
    }

    [TestMethod]
    public void Dedent_RetireUnNiveau()
    {
        Assert.AreEqual("    ", IndentationRules.Dedent("        ", Unit));
        Assert.AreEqual("", IndentationRules.Dedent("  ", Unit));
    }

    [TestMethod]
    public void IsEmptyPair_DetecteLesPaires()
    {
        Assert.IsTrue(IndentationRules.IsEmptyPair('{', '}'));
        Assert.IsFalse(IndentationRules.IsEmptyPair('{', ')'));
    }
}