namespace Game.Managers;

public class ContentInfo
{
    public string AbsolutePath { get; set; }

    public string ContentPath { get; set; }

    public MemoryStream ContentStream
    {
        get => field is not null ? field : throw new InvalidOperationException("ContentStream hasn't been initialized");
        set;
    } = null!;

    public string Filename { get; set; }


    public ContentInfo(string absolutePath)
    {
        AbsolutePath = absolutePath;
        var pos = absolutePath.LastIndexOf('.');
        ContentPath = pos > -1 ? absolutePath[..pos] : absolutePath;
        Filename = Path.GetFileName(AbsolutePath);
    }

    public void SetContentStream(Stream stream)
    {
        if (stream is MemoryStream contentStream)
        {
            ContentStream = contentStream;
            ContentStream.Position = 0L;
        }
        else
        {
            throw new Exception("Can't set ContentStream width type " + stream.GetType().Name);
        }
    }

    public Stream Duplicate()
    {
        if (ContentStream is not { CanRead: true, CanWrite: true })
        {
            throw new Exception("ContentStream has been disposed");
        }

        var memoryStream = new MemoryStream();
        ContentStream.CopyTo(memoryStream);
        ContentStream.Position = 0L;
        memoryStream.Position = 0L;
        return memoryStream;
    }

    public void Dispose()
    {
        ContentStream?.Dispose();
    }
}
