using IndustrialSentinel.Core.Security;
using IndustrialSentinel.Infrastructure.Persistence;
using IndustrialSentinel.Infrastructure.Security;
using Xunit;

namespace IndustrialSentinel.Tests;

public class SqliteSmokeTests
{
    [Fact]
    public void SqliteSchema_CreatesUsersAndPersistsPasswordChange()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"industrial_sentinel_{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(dbPath, busyTimeoutMs: 1000, useWal: false);
            using (var connection = factory.OpenConnection())
            {
                SqliteSchema.EnsureCreated(connection);
            }

            var users = new UserRepository(factory);
            users.CreateUser("admin", "Aa1!StrongPass", UserRole.Admin);
            var user = users.GetUser("admin");

            Assert.NotNull(user);
            Assert.NotNull(user!.PasswordChangedUtc);
        }
        finally
        {
            TryDelete(dbPath);
            TryDelete(dbPath + "-wal");
            TryDelete(dbPath + "-shm");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}
