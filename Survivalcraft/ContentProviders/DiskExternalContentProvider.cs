namespace Game.ContentProviders;

public class DiskExternalContentProvider : IExternalContentProvider
{
    public string DisplayName => LanguageManager.Get(_typeName, "DisplayName");

    private const string _typeName = nameof(DiskExternalContentProvider);

    public bool SupportsLinks => false;

    public bool SupportsListing => false;

    public bool RequiresLogin => false;

    public bool IsLoggedIn => true;

    public const string LocalPath = "external:";

    public string Description => "No login required; Save to disk";

    public void Logout()
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
    }

    public void Download(string path, CancellableProgress progress, Action<Stream> success, Action<Exception> failure)
    {
        failure(new NotSupportedException());
    }

    public void Upload(string path, Stream stream, CancellableProgress progress, Action<string> success,
        Action<Exception> failure)
    {
        ThreadPool.QueueUserWorkItem(
            delegate
            {
                try
                {
                    var destinationPath = Path.Combine(LocalPath, path);
                    using (var destination = Storage.OpenFile(destinationPath, OpenFileMode.Create))
                    {
                        stream.CopyTo(destination);
                    }

                    Dispatcher.Dispatch(delegate { success(destinationPath); });
                }
                catch (Exception ex)
                {
                    Dispatcher.Dispatch(delegate { failure(ex); });
                }
            }
        );
    }

    public void Link(string path, CancellableProgress progress, Action<string> success, Action<Exception> failure)
    {
        failure(new NotSupportedException());
    }

    public void List(string path, CancellableProgress progress, Action<ExternalContentEntry> success,
        Action<Exception> failure)
    {
        ThreadPool.QueueUserWorkItem(
            delegate
            {
                try
                {
                    var internalPath = path;
                    var entry = GetDirectoryEntry(internalPath, true);
                    success(entry);
                }
                catch (Exception ex)
                {
                    failure(ex);
                }
            }
        );
    }

    public void Login(CancellableProgress progress, Action success, Action<Exception> failure)
    {
        failure(new NotSupportedException());
    }

    private ExternalContentEntry GetDirectoryEntry(string internalPath, bool scanContents)
    {
        ExternalContentEntry externalContentEntry = new()
        {
            Type = ExternalContentType.Directory, Path = internalPath, Time = new DateTime(1970, 1, 1)
        };
        if (!scanContents)
        {
            return externalContentEntry;
        }

        if (internalPath is [_, ':'])
        {
            internalPath += '/';
        }

        var directories = Directory.GetDirectories(internalPath);
        foreach (var internalPath2 in directories)
        {
            externalContentEntry.ChildEntries.Add(GetDirectoryEntry(internalPath2, false));
        }

        directories = Directory.GetFiles(internalPath);
        foreach (var text in directories)
        {
            FileInfo fileInfo = new(text);
            ExternalContentEntry externalContentEntry2 = new()
            {
                Type = ExternalContentManager.ExtensionToType(Path.GetExtension(text)),
                Path = text,
                Size = fileInfo.Length,
                Time = fileInfo.CreationTime
            };
            externalContentEntry.ChildEntries.Add(externalContentEntry2);
        }

        return externalContentEntry;
    }
}
