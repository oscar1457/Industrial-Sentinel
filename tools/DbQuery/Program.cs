using Microsoft.Data.Sqlite;

var dbPath = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.OrdinalIgnoreCase))
    ?? Environment.ExpandEnvironmentVariables("%LocalAppData%\\IndustrialSentinel\\industrial_sentinel.db");
var deleteUsers = args.Any(arg => string.Equals(arg, "--delete-users", StringComparison.OrdinalIgnoreCase));
if (!File.Exists(dbPath))
{
    Console.WriteLine($"DB no existe: {dbPath}");
    return 1;
}

using var connection = new SqliteConnection($"Data Source={dbPath};");
connection.Open();
using var cmd = connection.CreateCommand();

if (deleteUsers)
{
    cmd.CommandText = "DELETE FROM users;";
    var rows = cmd.ExecuteNonQuery();
    Console.WriteLine($"Usuarios eliminados: {rows}");
    return 0;
}

cmd.CommandText = "select id, username, role, is_locked, lockout_until_utc, password_changed_utc from users order by username;";
using var reader = cmd.ExecuteReader();

while (reader.Read())
{
    var id = reader.GetInt64(0);
    var user = reader.GetString(1);
    var role = reader.GetString(2);
    var locked = reader.GetInt32(3);
    var lockout = reader.IsDBNull(4) ? "" : reader.GetString(4);
    var changed = reader.IsDBNull(5) ? "" : reader.GetString(5);
    Console.WriteLine($"{id} | {user} | {role} | locked:{locked} | lockout:{lockout} | changed:{changed}");
}

return 0;
