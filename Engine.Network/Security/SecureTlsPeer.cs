using System.Security.Cryptography.X509Certificates;

using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

using CertificateRequest = Org.BouncyCastle.Tls.CertificateRequest;

namespace Engine.Network.Security;

internal static class SecureTlsPeer
{
    // DTLS 1.2 ChaCha20-Poly1305 uses 29 bytes; reserve another 8 bytes of MTU headroom.
    internal const int recordBudget = 37;

    internal static readonly SignatureAndHashAlgorithm signature = new(HashAlgorithm.sha256, SignatureAlgorithm.ecdsa);

    internal static TlsCredentials Credentials(NetworkIdentity identity, BcTlsCrypto crypto, TlsContext context)
    {
        var key = identity.ExportPrivateKey();
        try
        {
            return new BcDefaultTlsCredentialedSigner(
                new TlsCryptoParameters(context),
                crypto,
                PrivateKeyFactory.CreateKey(key),
                new Certificate([crypto.CreateCertificate(identity.Certificate.ToArray())]),
                signature
            );
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    internal static NetworkPeerIdentity Identity(Certificate certificate)
    {
        if (certificate.IsEmpty || certificate.Length != 1)
        {
            throw new TlsFatalAlert(AlertDescription.bad_certificate);
        }

        using var value = X509CertificateLoader.LoadCertificate(certificate.GetCertificateAt(0).GetEncoded());
        using var key = value.GetECDsaPublicKey() ?? throw new TlsFatalAlert(AlertDescription.bad_certificate);
        return new NetworkPeerIdentity(key.ExportSubjectPublicKeyInfo());
    }

    internal sealed class Client(
        NetworkIdentity identity,
        BcTlsCrypto crypto,
        Action<NetworkPeerIdentity> verify,
        Action<byte[]> binding
    )
        : DefaultTlsClient(crypto), TlsAuthentication
    {
        protected override ProtocolVersion[] GetSupportedVersions() => [ProtocolVersion.DTLSv12];

        protected override int[] GetSupportedCipherSuites() =>
            [CipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256];

        public override int GetHandshakeTimeoutMillis() => 5000;

        public override int GetMaxHandshakeMessageSize() => 4096;

        public override bool RequiresExtendedMasterSecret() => true;

        public override TlsAuthentication GetAuthentication() => this;

        public void NotifyServerCertificate(TlsServerCertificate certificate)
        {
            verify(Identity(certificate.Certificate));
        }

        public TlsCredentials GetClientCredentials(CertificateRequest request)
        {
            return Credentials(identity, (BcTlsCrypto)Crypto, m_context);
        }

        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();
            binding(m_context.ExportKeyingMaterial("EXPORTER-SCNET-transport-v1", [], 32));
        }
    }

    internal sealed class Server(
        NetworkIdentity identity,
        BcTlsCrypto crypto,
        Action<NetworkPeerIdentity> verify,
        Action<byte[]> binding
    )
        : DefaultTlsServer(crypto)
    {
        protected override ProtocolVersion[] GetSupportedVersions() => [ProtocolVersion.DTLSv12];

        protected override int[] GetSupportedCipherSuites() =>
            [CipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256];

        public override int GetHandshakeTimeoutMillis() => 5000;

        public override int GetMaxHandshakeMessageSize() => 4096;

        public override bool RequiresExtendedMasterSecret() => true;

        protected override TlsCredentialedSigner GetECDsaSignerCredentials()
        {
            return (TlsCredentialedSigner)Credentials(identity, (BcTlsCrypto)Crypto, m_context);
        }

        public override CertificateRequest GetCertificateRequest()
        {
            return new([ClientCertificateType.ecdsa_sign], new List<SignatureAndHashAlgorithm> { signature }, null);
        }

        public override void NotifyClientCertificate(Certificate certificate) => verify(Identity(certificate));

        public override void NotifyHandshakeComplete()
        {
            base.NotifyHandshakeComplete();
            binding(m_context.ExportKeyingMaterial("EXPORTER-SCNET-transport-v1", [], 32));
        }
    }
}
