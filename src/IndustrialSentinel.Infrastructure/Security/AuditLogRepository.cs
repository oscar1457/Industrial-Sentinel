using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace IndustrialSentinel.Infrastructure.Security;

public sealed class AuditLogRepository
{
    private readonly SqliteConnectionFactory _factory;

    public AuditLogRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public string? GetLastHash()
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT hash FROM audit_logs ORDER BY id DESC LIMIT 1;";
        return cmd.ExecuteScalar() as string;
    }

    public void Add(string username, string action, string details)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        var prevHash = GetLastHash() ?? string.Empty;
        var payload = $"{timestamp}|{username}|{action}|{details}|{prevHash}";
        var hash = ComputeHash(payload);

        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO audit_logs (timestamp_utc, username, action, details, prev_hash, hash)
VALUES ($ts, $user, $action, $details, $prev, $hash);
";
        cmd.Parameters.AddWithValue("$ts", timestamp);
        cmd.Parameters.AddWithValue("$user", username);
        cmd.Parameters.AddWithValue("$action", action);
        cmd.Parameters.AddWithValue("$details", details);
        cmd.Parameters.AddWithValue("$prev", prevHash);
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.ExecuteNonQuery();
    }

    public void ExportToCsv(string path)
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT timestamp_utc, username, action, details, prev_hash, hash
FROM audit_logs
ORDER BY id ASC;
";
        using var reader = cmd.ExecuteReader();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        using var writer = new StreamWriter(path, false, Encoding.UTF8);
        writer.WriteLine("timestamp_utc,username,action,details,prev_hash,hash");
        while (reader.Read())
        {
            var line = string.Join(',',
                Escape(reader.GetString(0)),
                Escape(reader.GetString(1)),
                Escape(reader.GetString(2)),
                Escape(reader.GetString(3)),
                Escape(reader.GetString(4)),
                Escape(reader.GetString(5)));
            writer.WriteLine(line);
        }
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return '"' + value.Replace("\"", "\"\"") + '"';
        }

        return value;
    }

    private static string ComputeHash(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
