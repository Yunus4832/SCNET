namespace Game.Content;

public class ContentServerClientFactory
{
    public virtual ContentServerClient Create(ContentRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var normalized = repository.Normalize();
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            MaxResponseHeadersLength = 64
        };
        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = ContentServerClient.MaximumJsonResponseBytes
        };
        return new ContentServerClient(normalized.BaseUrl, httpClient, true);
    }
}
