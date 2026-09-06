namespace Game.Content;

public class ContentServerClientFactory
{
    public virtual ContentServerClient Create(ContentRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new ContentServerClient(repository.Normalize().BaseUrl);
    }
}
