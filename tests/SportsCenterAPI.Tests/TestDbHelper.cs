using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsCenterAPI.Data;

namespace SportsCenterAPI.Tests;

public static class TestDbHelper
{
    public static AppDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new AppDbContext(options);
    }
}
// SQLite trong bộ nhớ hỗ trợ ExecuteUpdateAsync và transaction, nên dùng cho test xoay token và thanh toán.
public sealed class SqliteTestDb : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = new("DataSource=:memory:");
    public SqliteTestDb() => _connection.Open();

    public AppDbContext CreateContext()
    {
        var db = new SqliteAppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose() => _connection.Dispose();
}

// SQLite không có sequence của Postgres; bỏ nó khỏi model để tạo schema, test không dùng đến.
file sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Model.RemoveSequence("PayOSOrderNumbers");
    }
}
