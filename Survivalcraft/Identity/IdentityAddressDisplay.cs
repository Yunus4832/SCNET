namespace Game.Identity;

/// <summary>Display only. Never use an abbreviated value for identity comparisons or serialization.</summary>
public static class IdentityAddressDisplay
{
    public static string Abbreviate(string address)
    {
        const string prefix = "scnet1";
        return address.StartsWith(prefix, StringComparison.Ordinal) && address.Length > prefix.Length + 15
            ? address[..(prefix.Length + 6)] + "..." + address[^6..]
            : address;
    }
}
