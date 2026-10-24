using System.Security.Cryptography;

using Engine.Network.Security;

namespace Game.Identity;

/// <summary>Password-protected PKCS#8 identity backups.</summary>
public static class IdentityBackup
{
    public static byte[] Export(NetworkIdentity identity, ReadOnlySpan<char> password)
    {
        ValidatePassword(password);
        var bytes = identity.ExportPrivateKey();
        try
        {
            using var key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(bytes, out _);
            return key.ExportEncryptedPkcs8PrivateKey(
                password,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 250000)
            );
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public static NetworkIdentity Import(ReadOnlySpan<byte> backup, ReadOnlySpan<char> password)
    {
        return DecryptBackup(backup, password);
    }

    private static NetworkIdentity DecryptBackup(ReadOnlySpan<byte> backup, ReadOnlySpan<char> password)
    {
        ValidatePassword(password);
        if (backup.IsEmpty || backup.Length > 16384)
        {
            throw new ArgumentException("Invalid encrypted identity backup size.", nameof(backup));
        }

        using var key = ECDsa.Create();
        key.ImportEncryptedPkcs8PrivateKey(password, backup, out var consumed);
        if (consumed != backup.Length) { throw new CryptographicException("Identity backup contains trailing data."); }

        var bytes = key.ExportPkcs8PrivateKey();
        try { return NetworkIdentity.Import(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private static void ValidatePassword(ReadOnlySpan<char> password)
    {
        if (password.Length < 8)
        {
            throw new ArgumentException(
                "Identity backup passwords must contain at least eight characters.",
                nameof(password)
            );
        }
    }
}
