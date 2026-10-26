using System.Security.Cryptography;

namespace Game.Admission;

/// <summary>Server-local shared password verifier. It never stores or advertises the password.</summary>
internal sealed class ServerPasswordVerifier
{
    private const int _iterations = 600000;

    private readonly byte[] _salt;

    private readonly byte[] _hash;

    internal ServerPasswordVerifier(string salt, string hash)
    {
        _salt = Convert.FromBase64String(salt);
        _hash = Convert.FromBase64String(hash);
        if (_salt.Length != 16 || _hash.Length != 32)
        {
            throw new InvalidDataException("Invalid server password verifier.");
        }
    }

    internal string Salt => Convert.ToBase64String(_salt);

    internal string Hash => Convert.ToBase64String(_hash);

    internal static ServerPasswordVerifier Create(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024)
        {
            throw new ArgumentException("Server passwords require 1 to 1024 characters.", nameof(password));
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, 32);
        try { return new ServerPasswordVerifier(Convert.ToBase64String(salt), Convert.ToBase64String(hash)); }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }

    internal bool Verify(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 1024) { return false; }

        var hash = Rfc2898DeriveBytes.Pbkdf2(password, _salt, _iterations, HashAlgorithmName.SHA256, 32);
        try { return CryptographicOperations.FixedTimeEquals(hash, _hash); }
        finally { CryptographicOperations.ZeroMemory(hash); }
    }
}
