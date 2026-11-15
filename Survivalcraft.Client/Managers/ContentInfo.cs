namespace Game.Managers;

/// <summary>Owned immutable content bytes with independent read cursors for background preparation.</summary>
public class ContentInfo : IDisposable
{
    private sealed record ContentBytes(byte[] Data, int Offset, int Count);

    private ContentBytes? _bytes;

    public string AbsolutePath { get; set; }

    public string ContentPath { get; set; }

    public string Filename { get; set; }

    public bool IsDisposed => Volatile.Read(ref _bytes) == null;

    public ContentInfo(string absolutePath)
    {
        AbsolutePath = absolutePath;
        var pos = absolutePath.LastIndexOf('.');
        ContentPath = pos > -1 ? absolutePath[..pos] : absolutePath;
        Filename = Path.GetFileName(absolutePath);
    }

    /// <summary>Takes ownership of the stream and freezes its complete content.</summary>
    public void SetContentStream(Stream stream)
    {
        using (stream)
        {
            if (stream is MemoryStream memory)
            {
                _bytes = memory.TryGetBuffer(out var buffer)
                    ? new ContentBytes(buffer.Array!, buffer.Offset, checked((int)memory.Length))
                    : new ContentBytes(memory.ToArray(), 0, checked((int)memory.Length));
            }
            else
            {
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                _bytes = new ContentBytes(copy.ToArray(), 0, checked((int)copy.Length));
            }
        }
    }

    public Stream Duplicate() => Open(Capture());

    public Func<Stream> CreateStreamFactory()
    {
        var bytes = Capture();
        return () => Open(bytes);
    }

    private ContentBytes Capture() =>
        Volatile.Read(ref _bytes) ?? throw new ObjectDisposedException(nameof(ContentInfo));

    private static Stream Open(ContentBytes bytes) =>
        new MemoryStream(bytes.Data, bytes.Offset, bytes.Count, false, false);

    public void Dispose() => Interlocked.Exchange(ref _bytes, null);
}
