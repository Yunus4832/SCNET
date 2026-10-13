namespace Engine.Network;

public static class RendezvousAddress
{
    public static Uri Normalize(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || address.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(address.UserInfo) || !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment) || address.Scheme == "http" && !address.IsLoopback)
        {
            throw new ArgumentException("Expected an HTTPS control address; HTTP is allowed only on loopback.", nameof(address));
        }
        return new Uri(address.AbsoluteUri.TrimEnd('/') + '/');
    }
}
