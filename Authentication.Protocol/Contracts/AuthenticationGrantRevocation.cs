namespace SCNET.Authentication.Protocol.Contracts;

public sealed record AuthenticationGrantRevocation(string Grant)
{
    public override string ToString() => "AuthenticationGrantRevocation { credentials redacted }";
}
