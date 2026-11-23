using KEYCE_KODER.Core.Completion;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class SnippetTests
{
    [TestMethod]
    public void Expand_RemplaceLIndentationEtPlaceLeCurseur()
    {
        (string text, int caret) = SnippetExpander.Expand("if $0:\n\tpass", "", "    ");

        Assert.AreEqual("if :\n    pass", text);
        Assert.AreEqual(3, caret);
    }

    [TestMethod]
    public void Expand_AjouteLIndentationDeLaLigneActuelle()
    {
        (string text, _) = SnippetExpander.Expand("if $0:\n\tpass", "    ", "    ");

        Assert.AreEqual("if :\n        pass", text);
    }

    [TestMethod]
    public void Expand_SansMarqueur_CurseurALaFin()
    {
        (string text, int caret) = SnippetExpander.Expand("pass", "", "    ");

        Assert.AreEqual(text.Length, caret);
    }

    [TestMethod]
    public void GetCompletions_ProposeLeSnippetMemeSiLeMotEstComplet()
    {
        var items = new CompletionService().GetCompletions("for", 3, "main.py");

        Assert.IsTrue(items.Any(i => i.Kind == CompletionKind.Snippet && i.Text == "for"));
    }

    [TestMethod]
    public void GetCompletions_ProposeLeMainJava()
    {
        var items = new CompletionService().GetCompletions("ma", 2, "Main.java");

        CompletionItem main = items.First(i => i.Kind == CompletionKind.Snippet && i.Text == "main");
        StringAssert.Contains(main.SnippetBody, "public static void main");
    }
}