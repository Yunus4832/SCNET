using System.Security.Cryptography;

using Engine.Core.Security;

using Microsoft.IdentityModel.Tokens;

namespace SCNET.Authentication.Protocol.Security;

/// <summary>A P-256 public key identified by its complete RFC 7638 thumbprint.</summary>
public sealed class AuthenticationPublicKey
{
    private readonly byte[] _subjectPublicKeyInfo;

    public AuthenticationPublicKey(ReadOnlySpan<byte> subjectPublicKeyInfo)
    {
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out var consumed);
        if (consumed != subjectPublicKeyInfo.Length ||
            key.ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
        {
            throw new CryptographicException("Authentication requires exactly one P-256 public key.");
        }

        _subjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();
        Address = IdentityAddress.Encode(new ECDsaSecurityKey(key).ComputeJwkThumbprint());
    }

    public string Address { get; }

    public string EncodedKey => Base64UrlEncoder.Encode(_subjectPublicKeyInfo);

    public static AuthenticationPublicKey From(ECDsa key) => new(key.ExportSubjectPublicKeyInfo());

    public static AuthenticationPublicKey Parse(string encodedKey)
    {
        if (string.IsNullOrEmpty(encodedKey) || encodedKey.Length > 256)
        {
            throw new FormatException("Invalid authentication public key length.");
        }

        return new AuthenticationPublicKey(Base64UrlEncoder.DecodeBytes(encodedKey));
    }

    public ECDsa CreateVerifier()
    {
        var key = ECDsa.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(_subjectPublicKeyInfo, out _);
            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }
}
