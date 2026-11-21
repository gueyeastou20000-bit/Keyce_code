
using KEYCE_KODER.Core.Services;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class FileServiceTests
{
    [TestMethod]
    public async Task ReadFileAsync_ShouldReturnFileContent()
    {
        // Arrange
        string path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.txt"
        );

        await File.WriteAllTextAsync(path, "Bonjour KEYCE KODER");

        var service = new FileService();

        try
        {
            // Act
            string content = await service.ReadFileAsync(path);

            // Assert
            Assert.AreEqual("Bonjour KEYCE KODER", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task SaveFileAsync_ShouldCreateNewFile()
    {
        // Arrange
        string path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.txt"
        );

        var service = new FileService();

        try
        {
            // Act
            await service.SaveFileAsync(path, "Nouveau fichier");

            // Assert
            Assert.IsTrue(File.Exists(path));

            string content = await File.ReadAllTextAsync(path);
            Assert.AreEqual("Nouveau fichier", content);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public async Task SaveFileAsync_ShouldOverwriteExistingContent()
    {
        // Arrange
        string path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.txt"
        );

        await File.WriteAllTextAsync(path, "Ancien contenu");

        var service = new FileService();

        try
        {
            // Act
            await service.SaveFileAsync(path, "Nouveau contenu");

            // Assert
            string content = await File.ReadAllTextAsync(path);

            Assert.AreEqual("Nouveau contenu", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ReadFileAsync_WhenFileDoesNotExist_ShouldThrowException()
    {
        // Arrange
        string path = Path.Combine(
            Path.GetTempPath(),
            $"{Guid.NewGuid()}.txt"
        );

        var service = new FileService();

        // Act et Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.ReadFileAsync(path)
        );
    }
}
