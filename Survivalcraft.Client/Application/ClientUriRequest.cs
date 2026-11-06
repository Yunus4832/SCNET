namespace Game.Application;

public sealed class ClientUriRequest(Uri uri)
{
    public Uri Uri { get; } = uri;

    public bool IsHandled { get; set; }
}
