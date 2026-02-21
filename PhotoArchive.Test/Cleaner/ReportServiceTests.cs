using PhotoArchive.Cleaner.Services;

namespace PhotoArchive.Test.Cleaner;

public class ReportServiceTests
{
    [Fact]
    public void RegisterMethods_UpdateCounters()
    {
        var report = new ReportService();

        report.RegisterImage("a.jpg");
        report.RegisterDuplicate("b.jpg");
        report.RegisterOther("c.txt");

        Assert.Equal(1, report.Images);
        Assert.Equal(1, report.Duplicates);
        Assert.Equal(1, report.Others);
        Assert.Equal(3, report.Total);
    }

    [Fact]
    public void PrintSummary_WritesExpectedSections()
    {
        var report = new ReportService();
        report.RegisterImage("a.jpg");
        report.RegisterOther("b.txt");

        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            report.PrintSummary();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = writer.ToString();
        Assert.Contains("Cleaning complete:", output, StringComparison.Ordinal);
        Assert.Contains("Images: 1", output, StringComparison.Ordinal);
        Assert.Contains("Others: 1", output, StringComparison.Ordinal);
    }
}
