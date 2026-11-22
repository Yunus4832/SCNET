namespace Game.Identity;

/// <summary>仅用于展示，身份比较和序列化必须使用完整值。</summary>
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
