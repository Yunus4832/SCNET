using System.Security.Cryptography;
using System.Text;

using Engine.Core.Security;

namespace Engine.Network.Security;

/// <summary>A peer's proven P-256 public key. Trust must be decided independently of proof of possession.</summary>
public sealed class NetworkPeerIdentity
{
    private readonly byte[] _publicKey;

    internal NetworkPeerIdentity(byte[] publicKey)
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(publicKey, out var read);
        if (read != publicKey.Length || key.KeySize != 256)
        {
            throw new CryptographicException("Transport identities require a P-256 public key.");
        }

        var parameters = key.ExportParameters(false);
        if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
        {
            throw new CryptographicException("Transport identity curve must be P-256.");
        }

        _publicKey = publicKey.ToArray();
        var json = "{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"" +
                   Encode(parameters.Q.X!) +
                   "\",\"y\":\"" +
                   Encode(parameters.Q.Y!) +
                   "\"}";
        Address = IdentityAddress.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    public string Address { get; }

    public ReadOnlyMemory<byte> PublicKey => _publicKey;

    private static string Encode(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
