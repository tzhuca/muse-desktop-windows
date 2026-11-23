using Muse.Core;
using Xunit;

namespace Muse.Core.Tests;

public class DownloadPathTests : IDisposable
{
    private readonly string _directory;

    public DownloadPathTests()
    {
        _directory = Directory.CreateTempSubdirectory("muse-download-").FullName;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A test may still hold a reserved file open; nothing else to do.
        }
    }

    [Fact]
    public void ReservesDistinctPathsForSimultaneousDownloadsBeforeEitherWritesData()
    {
        var first = DownloadPath.Reserve(_directory, "report.pdf");
        var second = DownloadPath.Reserve(_directory, "report.pdf");

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }

    [Fact]
    public void MakesASafeNonOverwritingDownloadName()
    {
        File.WriteAllText(Path.Combine(_directory, "report.pdf"), "existing");

        Assert.Equal(
            Path.Combine(_directory, "report (1).pdf"),
            DownloadPath.Reserve(_directory, "../../report.pdf"));
        Assert.Equal(
            Path.Combine(_directory, "report (2).pdf"),
            DownloadPath.Reserve(_directory, "report.pdf"));

        Assert.Equal("existing", File.ReadAllText(Path.Combine(_directory, "report.pdf")));
        Assert.Equal(0, new FileInfo(Path.Combine(_directory, "report (1).pdf")).Length);
    }

    [Fact]
    public void CancelsDownloadsWhenTheDestinationCannotBeReserved()
    {
        var missing = Path.Combine(_directory, "missing");
        Assert.ThrowsAny<IOException>(() => DownloadPath.Reserve(missing, "report.pdf"));
        Assert.False(Directory.Exists(missing));
    }

    [Theory]
    [InlineData("../../report.pdf", "report.pdf")]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData(null, DownloadPath.FallbackFileName)]
    [InlineData("", DownloadPath.FallbackFileName)]
    [InlineData("   ", DownloadPath.FallbackFileName)]
    public void StripsPathsAndFallsBackWhenTheNameIsMissing(string? input, string expected)
    {
        Assert.Equal(expected, DownloadPath.SanitizeFileName(input));
    }

    [Theory]
    [InlineData("report?.pdf", "report_.pdf")]
    [InlineData("report|.pdf", "report_.pdf")]
    [InlineData("report:.pdf", "report_.pdf")]
    public void ReplacesCharactersWindowsRejects(string input, string expected)
    {
        Assert.Equal(expected, DownloadPath.SanitizeFileName(input));
    }
}
