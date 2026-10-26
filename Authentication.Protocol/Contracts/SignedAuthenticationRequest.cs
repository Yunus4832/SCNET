namespace SCNET.Authentication.Protocol.Contracts;

/// <summary>The public key is only trusted after verifying the proof with the same key.</summary>
public sealed record SignedAuthenticationRequest(string PublicKey, string Proof)
{
    public override string ToString() => "SignedAuthenticationRequest { credentials redacted }";
}
