
using KEYCE_KODER.Core.Models;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class DocumentTests
{
    [TestMethod]
    public void NewDocument_ShouldStartAsClean()
    {
        // Arrange et Act
        var document = new Document();

        // Assert
        Assert.IsFalse(document.IsDirty);
        Assert.AreEqual("", document.Content);
        Assert.IsNull(document.FilePath);
        Assert.AreEqual("Sans titre", document.Name);
    }

    [TestMethod]
    public void ChangingContent_ShouldMarkDocumentAsDirty()
    {
        // Arrange
        var document = new Document();

        // Act
        document.Content = "Console.WriteLine(\"Bonjour\");";

        // Assert
        Assert.IsTrue(document.IsDirty);
    }

    [TestMethod]
    public void SettingSameContent_ShouldNotMarkDocumentAsDirty()
    {
        // Arrange
        var document = new Document(content: "Bonjour");

        // Act
        document.Content = "Bonjour";

        // Assert
        Assert.IsFalse(document.IsDirty);
    }

    [TestMethod]
    public void MarkSaved_ShouldResetDirtyState()
    {
        // Arrange
        var document = new Document();
        document.Content = "Nouveau contenu";

        // Act
        document.MarkSaved();

        // Assert
        Assert.IsFalse(document.IsDirty);
    }

    [TestMethod]
    public void MarkSavedWithPath_ShouldUpdateFilePath()
    {
        // Arrange
        var document = new Document();
        string path = Path.Combine(
            Path.GetTempPath(),
            "mon-fichier.txt"
        );

        document.Content = "Contenu à enregistrer";

        // Act
        document.MarkSaved(path);

        // Assert
        Assert.AreEqual(path, document.FilePath);
        Assert.AreEqual("mon-fichier.txt", document.Name);
        Assert.IsFalse(document.IsDirty);
    }
}
