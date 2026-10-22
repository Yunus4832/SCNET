using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Engine.Network.Security;

/// <summary>Caller-owned persistent transport identity. Exported private keys must be protected by the caller.</summary>
public sealed class NetworkIdentity : IDisposable
{
    private readonly Lock _gate = new();

    private readonly byte[] _privateKey;

    private readonly byte[] _certificate;

    private bool _disposed;

    private NetworkIdentity(byte[] privateKey)
    {
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(privateKey, out var read);
        if (read != privateKey.Length)
        {
            throw new CryptographicException("Invalid transport private key.");
        }

        PublicIdentity = new NetworkPeerIdentity(key.ExportSubjectPublicKeyInfo());
        var request = new CertificateRequest("CN=SCNET transport", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(5)
        );
        _privateKey = key.ExportPkcs8PrivateKey();
        _certificate = certificate.Export(X509ContentType.Cert);
    }

    public NetworkPeerIdentity PublicIdentity { get; }

    internal ReadOnlyMemory<byte> Certificate => _certificate;

    public static NetworkIdentity Create()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var bytes = key.ExportPkcs8PrivateKey();
        try
        {
            return new NetworkIdentity(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static NetworkIdentity Import(ReadOnlySpan<byte> privateKey)
    {
        var bytes = privateKey.ToArray();
        try
        {
            return new NetworkIdentity(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public byte[] ExportPrivateKey()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _privateKey.ToArray();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) { return; }

            _disposed = true;
            CryptographicOperations.ZeroMemory(_privateKey);
        }
    }
}
