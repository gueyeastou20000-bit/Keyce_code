
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class DocumentManagerTests
{
    [TestMethod]
    public void CreateDocument_ShouldAddNewEmptyDocument()
    {
        // Arrange
        var manager = new DocumentManager();

        // Act
        Document document = manager.CreateDocument();

        // Assert
        Assert.AreEqual(1, manager.Documents.Count);
        Assert.IsNull(document.FilePath);
        Assert.AreEqual("Sans titre", document.Name);
        Assert.IsFalse(document.IsDirty);
    }

    [TestMethod]
    public void OpenDocument_ShouldAddDocument()
    {
        // Arrange
        var manager = new DocumentManager();
        string path = Path.Combine(
            Path.GetTempPath(),
            "test-document.txt"
        );

        // Act
        Document document = manager.OpenDocument(path, "Bonjour");

        // Assert
        Assert.AreEqual(1, manager.Documents.Count);
        Assert.AreEqual("Bonjour", document.Content);
        Assert.AreEqual(Path.GetFullPath(path), document.FilePath);
        Assert.IsFalse(document.IsDirty);
    }

    [TestMethod]
    public void OpenDocument_TwiceWithSamePath_ShouldReturnSameDocument()
    {
        // Arrange
        var manager = new DocumentManager();
        string path = Path.Combine(
            Path.GetTempPath(),
            "duplicate-test.txt"
        );

        // Act
        Document first = manager.OpenDocument(path, "Premier contenu");
        Document second = manager.OpenDocument(path, "Deuxième contenu");

        // Assert
        Assert.AreSame(first, second);
        Assert.AreEqual(1, manager.Documents.Count);
        Assert.AreEqual("Premier contenu", second.Content);
    }

    [TestMethod]
    public void GetDocument_ShouldReturnDocumentWhenIdExists()
    {
        // Arrange
        var manager = new DocumentManager();
        Document document = manager.CreateDocument();

        // Act
        Document? result = manager.GetDocument(document.Id);

        // Assert
        Assert.AreSame(document, result);
    }

    [TestMethod]
    public void GetDocument_ShouldReturnNullWhenIdDoesNotExist()
    {
        // Arrange
        var manager = new DocumentManager();

        // Act
        Document? result = manager.GetDocument(Guid.NewGuid());

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void CloseDocument_ShouldRemoveExistingDocument()
    {
        // Arrange
        var manager = new DocumentManager();
        Document document = manager.CreateDocument();

        // Act
        bool result = manager.CloseDocument(document.Id);

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(0, manager.Documents.Count);
    }

    [TestMethod]
    public void CloseDocument_ShouldReturnFalseWhenDocumentDoesNotExist()
    {
        // Arrange
        var manager = new DocumentManager();

        // Act
        bool result = manager.CloseDocument(Guid.NewGuid());

        // Assert
        Assert.IsFalse(result);
    }
}
