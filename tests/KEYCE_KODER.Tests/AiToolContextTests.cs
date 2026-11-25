using KEYCE_KODER.AI.Tools.Execution;

namespace KEYCE_KODER.Tests;

[TestClass]
public sealed class AiToolContextTests
{
    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "KEYCE_KODER_TESTS",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    [TestMethod]
    public void ResolveProjectPath_ShouldAcceptPathInsideProject()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var context = new AiToolContext(directory);

            string result = context.ResolveProjectPath(
                Path.Combine("src", "Program.cs"));

            string expected = Path.GetFullPath(
                Path.Combine(directory, "src", "Program.cs"));

            Assert.AreEqual(expected, result);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ResolveProjectPath_ShouldRejectPathOutsideProject()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var context = new AiToolContext(directory);

            Assert.Throws<UnauthorizedAccessException>(
                () => context.ResolveProjectPath(
                    Path.Combine("..", "outside.txt")));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ResolveProjectPath_ShouldRejectAbsolutePath()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var context = new AiToolContext(directory);

            string absolutePath = Path.Combine(
                directory,
                "secret.txt");

            Assert.Throws<UnauthorizedAccessException>(
                () => context.ResolveProjectPath(absolutePath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void ResolveProjectPath_ShouldRejectEmptyPath()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            var context = new AiToolContext(directory);

            Assert.Throws<ArgumentException>(
                () => context.ResolveProjectPath(""));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Constructor_ShouldRejectNonexistentProjectDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(
            () => new AiToolContext(directory));
    }
}