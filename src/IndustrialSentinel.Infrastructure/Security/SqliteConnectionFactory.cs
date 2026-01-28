using Microsoft.Data.Sqlite;

namespace IndustrialSentinel.Infrastructure.Security;

public sealed class SqliteConnectionFactory
{
    private readonly string _databasePath;
    private readonly bool _useEncryption;
    private readonly byte[]? _key;
    private readonly int _busyTimeoutMs;
    private readonly bool _useWal;

    public SqliteConnectionFactory(string databasePath, byte[]? key = null, bool useEncryption = false, int busyTimeoutMs = 5000, bool useWal = true)
    {
        _databasePath = databasePath;
        _key = key;
        _useEncryption = useEncryption && key is not null;
        _busyTimeoutMs = Math.Max(0, busyTimeoutMs);
        _useWal = useWal;
    }

    public SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(_databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return OpenInternal(recovered: false);
    }

    private SqliteConnection OpenInternal(bool recovered)
    {
        try
        {
            var connection = new SqliteConnection($"Data Source={_databasePath};Cache=Shared;Mode=ReadWriteCreate;Pooling=True");
            connection.Open();

            if (_useEncryption && _key is not null)
            {
                ApplyEncryption(connection, _key);
            }

            ApplyPragmas(connection);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA user_version;";
            cmd.ExecuteScalar();

            return connection;
        }
        catch (SqliteException ex) when (!recovered && IsCorruption(ex))
        {
            // best-effort reset
            TryDelete(_databasePath + "-wal");
            TryDelete(_databasePath + "-shm");
            TryDelete(_databasePath);
            return OpenInternal(recovered: true);
        }
    }

    private static bool IsCorruption(SqliteException ex)
    {
        return ex.SqliteErrorCode is 26 or 7
            || ex.Message.Contains("file is not a database", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("file is encrypted", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("out of memory", StringComparison.OrdinalIgnoreCase);
    }

    private static void ApplyEncryption(SqliteConnection connection, byte[] key)
    {
        var hex = BitConverter.ToString(key).Replace("-", string.Empty);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA key = \"x'{hex}'\";";
        cmd.ExecuteNonQuery();
        using var cipherCmd = connection.CreateCommand();
        cipherCmd.CommandText = "PRAGMA cipher_compatibility = 4;";
        cipherCmd.ExecuteNonQuery();
    }

    private void ApplyPragmas(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout = {_busyTimeoutMs};";
        cmd.ExecuteNonQuery();

        if (_useWal)
        {
            cmd.CommandText = "PRAGMA journal_mode = WAL;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "PRAGMA synchronous = NORMAL;";
            cmd.ExecuteNonQuery();
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
