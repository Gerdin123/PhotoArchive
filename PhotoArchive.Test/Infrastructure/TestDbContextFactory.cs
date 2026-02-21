using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhotoArchive.Infrastructure;

namespace PhotoArchive.Test.Infrastructure;

internal sealed class TestDbContextScope(SqliteConnection connection, PhotoArchiveDbContext context) : IDisposable
{
    private readonly SqliteConnection _connection = connection;

    public PhotoArchiveDbContext Context { get; } = context;

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

internal static class TestDbContextFactory
{
    public static TestDbContextScope Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<PhotoArchiveDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new PhotoArchiveDbContext(options);
        context.Database.EnsureCreated();

        return new TestDbContextScope(connection, context);
    }
}
