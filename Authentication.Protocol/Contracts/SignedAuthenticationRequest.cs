namespace SCNET.Authentication.Protocol.Contracts;

/// <summary>使用该公钥验证持有证明后，才能信任该公钥。</summary>
public sealed record SignedAuthenticationRequest(string PublicKey, string Proof)
{
    public override string ToString() => "SignedAuthenticationRequest { credentials redacted }";
}
