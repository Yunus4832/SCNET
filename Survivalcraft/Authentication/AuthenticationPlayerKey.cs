using System.Security.Cryptography;

namespace Game.Authentication;

internal static class AuthenticationPlayerKey
{
    internal static ECDsa Open()
    {
        using var identity = LocalIdentityManager.Current.OpenPlayerIdentity();
        var bytes = identity.ExportPrivateKey();
        var key = ECDsa.Create();
        try
        {
            key.ImportPkcs8PrivateKey(bytes, out _);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
