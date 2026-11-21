
using KEYCE_KODER.Core.Models;
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class DocumentServiceTests
{
    [TestMethod]
    public void CreateDocument_ShouldCreateDocumentInManager()
    {
        // Arrange
        var manager = new DocumentManager();
        var service = new DocumentService(manager, new FileService());

        // Act
        Document document = service.CreateDocument();

        // Assert
        Assert.HasCount(1, manager.Documents);
        Assert.IsFalse(document.IsDirty);
    }

    [TestMethod]
    public async Task OpenDocumentAsync_ShouldReadFileContent()
    {
        // Arrange
        string path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.txt"
        );

        await File.WriteAllTextAsync(path, "Bonjour KEYCE KODER");

        var service = new DocumentService(
            new DocumentManager(),
            new FileService()
        );

        try
        {
            // Act
            Document document = await service.OpenDocumentAsync(path);

            // Assert
            Assert.AreEqual("Bonjour KEYCE KODER", document.Content);
            Assert.AreEqual(Path.GetFullPath(path), document.FilePath);
            Assert.IsFalse(document.IsDirty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task SaveDocumentAsync_ShouldSaveChanges()
    {
        // Arrange
        string path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.txt"
        );

        await File.WriteAllTextAsync(path, "Ancien contenu");

        var service = new DocumentService(
            new DocumentManager(),
            new FileService()
        );

        try
        {
            Document document = await service.OpenDocumentAsync(path);
            document.Content = "Nouveau contenu";

            // Act
            await service.SaveDocumentAsync(document);

            // Assert
            string savedContent = await File.ReadAllTextAsync(path);

            Assert.AreEqual("Nouveau contenu", savedContent);
            Assert.IsFalse(document.IsDirty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task SaveDocumentAsAsync_ShouldSaveToNewPath()
    {
        // Arrange
        string path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.txt"
        );

        var service = new DocumentService(
            new DocumentManager(),
            new FileService()
        );

        Document document = service.CreateDocument();
        document.Content = "Mon nouveau fichier";

        try
        {
            // Act
            await service.SaveDocumentAsAsync(document, path);

            // Assert
            string savedContent = await File.ReadAllTextAsync(path);

            Assert.AreEqual("Mon nouveau fichier", savedContent);
            Assert.AreEqual(Path.GetFullPath(path), document.FilePath);
            Assert.IsFalse(document.IsDirty);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public async Task SaveDocumentAsync_WithoutFilePath_ShouldThrowException()
    {
        // Arrange
        var service = new DocumentService(
            new DocumentManager(),
            new FileService()
        );

        Document document = service.CreateDocument();
        document.Content = "Contenu à sauvegarder";

        // Act et Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveDocumentAsync(document)
        );
    }
}
