using IndustrialSentinel.Core.Security;
using Microsoft.Data.Sqlite;

namespace IndustrialSentinel.Infrastructure.Security;

public sealed class UserRepository
{
    private readonly SqliteConnectionFactory _factory;

    public UserRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public bool HasAnyUsers()
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users;";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        return count > 0;
    }

    public IReadOnlyList<UserAccount> GetAll()
    {
        var users = new List<UserAccount>();
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT id, username, role, is_locked, lockout_until_utc, password_changed_utc
FROM users
ORDER BY username;
";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new UserAccount(
                reader.GetInt64(0),
                reader.GetString(1),
                Enum.Parse<UserRole>(reader.GetString(2), true),
                reader.GetInt32(3) == 1,
                reader.IsDBNull(4) ? (DateTime?)null : DateTime.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? (DateTime?)null : DateTime.Parse(reader.GetString(5))));
        }

        return users;
    }

    public UserAccount? GetUser(string username)
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT id, username, role, is_locked, lockout_until_utc, password_changed_utc
FROM users
WHERE username = $username;
";
        cmd.Parameters.AddWithValue("$username", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new UserAccount(
            reader.GetInt64(0),
            reader.GetString(1),
            Enum.Parse<UserRole>(reader.GetString(2), true),
            reader.GetInt32(3) == 1,
            reader.IsDBNull(4) ? (DateTime?)null : DateTime.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? (DateTime?)null : DateTime.Parse(reader.GetString(5)));
    }

    public (byte[] Hash, byte[] Salt, int FailedAttempts, DateTime? LockoutUntilUtc)? GetCredentials(string username)
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
SELECT password_hash, password_salt, failed_attempts, lockout_until_utc
FROM users
WHERE username = $username;
";
        cmd.Parameters.AddWithValue("$username", username);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var hash = (byte[])reader[0];
        var salt = (byte[])reader[1];
        var failed = reader.GetInt32(2);
        var lockout = reader.IsDBNull(3) ? (DateTime?)null : DateTime.Parse(reader.GetString(3));
        return (hash, salt, failed, lockout);
    }

    public void CreateUser(string username, string password, UserRole role)
    {
        var (hash, salt) = PasswordHasher.HashPassword(password);
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO users (username, password_hash, password_salt, role, failed_attempts, is_locked, password_changed_utc)
VALUES ($username, $hash, $salt, $role, 0, 0, $changed);
";
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.Add("$hash", SqliteType.Blob).Value = hash;
        cmd.Parameters.Add("$salt", SqliteType.Blob).Value = salt;
        cmd.Parameters.AddWithValue("$role", role.ToString());
        cmd.Parameters.AddWithValue("$changed", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void UpdatePassword(string username, string password)
    {
        var (hash, salt) = PasswordHasher.HashPassword(password);
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE users
SET password_hash = $hash, password_salt = $salt, password_changed_utc = $changed
WHERE username = $username;
";
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.Add("$hash", SqliteType.Blob).Value = hash;
        cmd.Parameters.Add("$salt", SqliteType.Blob).Value = salt;
        cmd.Parameters.AddWithValue("$changed", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void RegisterFailedAttempt(string username, int maxAttempts, TimeSpan lockout)
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE users
SET failed_attempts = failed_attempts + 1
WHERE username = $username;
";
        cmd.Parameters.AddWithValue("$username", username);
        cmd.ExecuteNonQuery();

        var failed = GetCredentials(username)?.FailedAttempts ?? 0;
        if (failed >= maxAttempts)
        {
            LockUser(username, DateTime.UtcNow.Add(lockout));
        }
    }

    public void ResetFailures(string username)
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE users
SET failed_attempts = 0, is_locked = 0, lockout_until_utc = NULL
WHERE username = $username;
";
        cmd.Parameters.AddWithValue("$username", username);
        cmd.ExecuteNonQuery();
    }

    private void LockUser(string username, DateTime untilUtc)
    {
        using var connection = _factory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
UPDATE users
SET is_locked = 1, lockout_until_utc = $until
WHERE username = $username;
";
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.AddWithValue("$until", untilUtc.ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
