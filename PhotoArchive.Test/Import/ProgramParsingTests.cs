using PhotoArchive.Import;

namespace PhotoArchive.Test.Import;

public class ProgramParsingTests
{
    [Fact]
    public void ParseCsvLine_HandlesQuotedCommas_AndEscapedQuotes()
    {
        var fields = ManifestCsv.ParseCsvLine("a,\"b,c\",\"d\"\"e\"");

        Assert.Equal(3, fields.Count);
        Assert.Equal("a", fields[0]);
        Assert.Equal("b,c", fields[1]);
        Assert.Equal("d\"e", fields[2]);
    }

    [Fact]
    public void NormalizeDatabasePath_AppendsDefaultFileName_ForDirectoryInput()
    {
        var result = ImportOptionsResolver.NormalizeDatabasePath("C:\\archive", "C:\\cleaned");

        Assert.Equal("C:\\archive\\photoarchive.db", result);
    }

    [Fact]
    public void IsDatabaseFileExtension_RecognizesSqliteExtensions()
    {
        var db = ImportOptionsResolver.IsDatabaseFileExtension(".db");
        var sqlite = ImportOptionsResolver.IsDatabaseFileExtension(".sqlite");
        var txt = ImportOptionsResolver.IsDatabaseFileExtension(".txt");

        Assert.True(db);
        Assert.True(sqlite);
        Assert.False(txt);
    }

    [Fact]
    public void ParseCsvLine_PreservesEmptyTrailingField()
    {
        var fields = ManifestCsv.ParseCsvLine("a,b,");

        Assert.Equal(3, fields.Count);
        Assert.Equal(string.Empty, fields[2]);
    }
}
