using Microsoft.Data.Sqlite;

namespace IndustrialSentinel.Infrastructure.Persistence;

public static class SqliteSchema
{
    private const int CurrentVersion = 2;

    public static void EnsureCreated(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS telemetry_samples (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT NOT NULL,
    rpm REAL NOT NULL,
    temperature_c REAL NOT NULL,
    vibration_mms REAL NOT NULL,
    rpm_smooth REAL NOT NULL,
    temperature_smooth REAL NOT NULL,
    vibration_smooth REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_telemetry_timestamp ON telemetry_samples(timestamp_utc);

CREATE TABLE IF NOT EXISTS alerts (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT NOT NULL,
    level TEXT NOT NULL,
    metric TEXT NOT NULL,
    value REAL NOT NULL,
    threshold REAL NOT NULL,
    message TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_alerts_timestamp ON alerts(timestamp_utc);

CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL UNIQUE COLLATE NOCASE,
    password_hash BLOB NOT NULL,
    password_salt BLOB NOT NULL,
    role TEXT NOT NULL,
    failed_attempts INTEGER NOT NULL DEFAULT 0,
    is_locked INTEGER NOT NULL DEFAULT 0,
    lockout_until_utc TEXT NULL,
    password_changed_utc TEXT NULL
);

CREATE TABLE IF NOT EXISTS audit_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT NOT NULL,
    username TEXT NOT NULL,
    action TEXT NOT NULL,
    details TEXT NOT NULL,
    prev_hash TEXT NOT NULL,
    hash TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_audit_timestamp ON audit_logs(timestamp_utc);
";
        cmd.ExecuteNonQuery();

        EnsureColumn(connection, "users", "password_changed_utc", "TEXT NULL");
        EnsurePasswordChangeStamp(connection);

        if (!IsIntegrityOk(connection))
        {
            throw new InvalidOperationException("SQLite integrity check failed.");
        }

        var version = GetUserVersion(connection);
        if (version > CurrentVersion)
        {
            throw new InvalidOperationException($"Database version {version} is newer than supported {CurrentVersion}.");
        }

        if (version < CurrentVersion)
        {
            SetUserVersion(connection, CurrentVersion);
        }
    }

    private static bool IsIntegrityOk(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA quick_check(1);";
        var result = cmd.ExecuteScalar()?.ToString();
        return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        if (ColumnExists(connection, table, column))
        {
            return;
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        cmd.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsurePasswordChangeStamp(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET password_changed_utc = COALESCE(password_changed_utc, $now);";
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
