using KEYCE_KODER.Core.Completion;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class CompletionServiceTests
{
    private readonly CompletionService _service = new();

    private static List<string> Texts(IReadOnlyList<CompletionItem> items) =>
        items.Select(i => i.Text).ToList();

    [TestMethod]
    public void GetPrefix_TrouveLeMotAvantLeCurseur()
    {
        (int start, string prefix) = CompletionService.GetPrefix("print(le", 8);

        Assert.AreEqual(6, start);
        Assert.AreEqual("le", prefix);
    }

    [TestMethod]
    public void GetCompletions_ProposeLesMotsClesPython()
    {
        var texts = Texts(_service.GetCompletions("de", 2, "main.py"));

        CollectionAssert.Contains(texts, "def");
        CollectionAssert.Contains(texts, "del");
    }

    [TestMethod]
    public void GetCompletions_ProposeLesMotsDuFichier()
    {
        string code = "maVariable = 1\nma";
        var texts = Texts(_service.GetCompletions(code, code.Length, "main.py"));

        CollectionAssert.Contains(texts, "maVariable");
    }

    [TestMethod]
    public void GetCompletions_NeProposePasLeMotEnCoursDeFrappe()
    {
        var texts = Texts(_service.GetCompletions("abcdef", 6, "notes.txt"));

        CollectionAssert.DoesNotContain(texts, "abcdef");
    }

    [TestMethod]
    public void GetCompletions_LangageInconnu_PasDeMotsCles()
    {
        var texts = Texts(_service.GetCompletions("de", 2, "notes.txt"));

        CollectionAssert.DoesNotContain(texts, "def");
    }

    [TestMethod]
    public void GetCompletions_IgnoreLaCasseDuPrefixe()
    {
        var texts = Texts(_service.GetCompletions("PRI", 3, "main.py"));

        CollectionAssert.Contains(texts, "print");
    }

    [TestMethod]
    public void Catalogue_ReconnaitLesLangagesParExtension()
    {
        Assert.AreEqual("Python", KEYCE_KODER.Core.Languages.LanguageCatalog.FromPath("a.py").DisplayName);
        Assert.AreEqual("TypeScript", KEYCE_KODER.Core.Languages.LanguageCatalog.FromPath("a.ts").DisplayName);
        Assert.AreEqual("Go", KEYCE_KODER.Core.Languages.LanguageCatalog.FromPath("a.go").DisplayName);
        Assert.AreEqual("Texte brut", KEYCE_KODER.Core.Languages.LanguageCatalog.FromPath("a.inconnu").DisplayName);
    }

    [TestMethod]
    public void GetCompletions_Php_GereLeDollar()
    {
        var texts = Texts(_service.GetCompletions("$_G", 3, "index.php"));

        CollectionAssert.Contains(texts, "$_GET");
    }

    [TestMethod]
    public void GetCompletions_Css_GereLeTiret()
    {
        var texts = Texts(_service.GetCompletions("background-c", 12, "style.css"));

        CollectionAssert.Contains(texts, "background-color");
    }

    [TestMethod]
    public void GetCompletions_Sql_IgnoreLaCasse()
    {
        var texts = Texts(_service.GetCompletions("sel", 3, "requete.sql"));

        CollectionAssert.Contains(texts, "SELECT");
    }
}