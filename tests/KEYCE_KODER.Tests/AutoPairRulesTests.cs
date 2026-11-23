using KEYCE_KODER.Core.Editing;
using KEYCE_KODER.Core.Languages;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class AutoPairRulesTests
{
    private static readonly LanguageDefinition Python = LanguageCatalog.FromPath("a.py");
    private static readonly LanguageDefinition Rust = LanguageCatalog.FromPath("a.rs");

    [TestMethod]
    public void Parenthese_EnFinDeLigne_InserePaire()
    {
        PairAction action = AutoPairRules.OnTyped('(', 'x', '\0', false, Python);

        Assert.AreEqual(PairActionKind.InsertPair, action.Kind);
        Assert.AreEqual(')', action.Close);
    }

    [TestMethod]
    public void Parenthese_DevantUnMot_PasDePaire()
    {
        PairAction action = AutoPairRules.OnTyped('(', ' ', 'a', false, Python);

        Assert.AreEqual(PairActionKind.None, action.Kind);
    }

    [TestMethod]
    public void Fermant_DevantLeMeme_LeSaute()
    {
        PairAction action = AutoPairRules.OnTyped(')', 'x', ')', false, Python);

        Assert.AreEqual(PairActionKind.SkipClosing, action.Kind);
    }

    [TestMethod]
    public void Apostrophe_ApresUneLettre_PasDePaire()
    {
        PairAction action = AutoPairRules.OnTyped('\'', 't', '\0', false, Python);

        Assert.AreEqual(PairActionKind.None, action.Kind);
    }

    [TestMethod]
    public void Guillemet_AvecSelection_Entoure()
    {
        PairAction action = AutoPairRules.OnTyped('"', ' ', 'a', true, Python);

        Assert.AreEqual(PairActionKind.WrapSelection, action.Kind);
        Assert.AreEqual('"', action.Close);
    }

    [TestMethod]
    public void Rust_ApostropheNEstPasUnGuillemet()
    {
        PairAction action = AutoPairRules.OnTyped('\'', ' ', '\0', false, Rust);

        Assert.AreEqual(PairActionKind.None, action.Kind);
    }

    [TestMethod]
    public void RetourArriere_EntreUnePaire_EffaceLesDeux()
    {
        Assert.IsTrue(AutoPairRules.ShouldDeletePair('(', ')', Python));
        Assert.IsTrue(AutoPairRules.ShouldDeletePair('"', '"', Python));
        Assert.IsFalse(AutoPairRules.ShouldDeletePair('(', 'a', Python));
    }
}