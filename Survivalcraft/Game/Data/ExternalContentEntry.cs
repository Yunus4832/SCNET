namespace Game;

public class ExternalContentEntry
{
    public readonly List<ExternalContentEntry> ChildEntries = [];

    public string Path = string.Empty;

    public long Size;

    public DateTime Time;

    public ExternalContentType Type;
}
