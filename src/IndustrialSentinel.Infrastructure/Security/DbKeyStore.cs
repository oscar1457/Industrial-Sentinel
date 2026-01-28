using System.Security.Cryptography;

namespace IndustrialSentinel.Infrastructure.Security;

public sealed class DbKeyStore
{
    private readonly string _path;

    public DbKeyStore(string path)
    {
        _path = path;
    }

    public byte[] GetOrCreateKey()
    {
        if (File.Exists(_path))
        {
            var protectedBytes = File.ReadAllBytes(_path);
            return ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
        var key = RandomNumberGenerator.GetBytes(32);
        var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_path, protectedKey);
        return key;
    }
}
