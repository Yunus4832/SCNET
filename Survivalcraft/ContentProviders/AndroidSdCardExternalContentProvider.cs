#if ANDROID

namespace Game.ContentProviders;

public class AndroidSdCardExternalContentProvider : IExternalContentProvider
{
    private const string _typeName = "AndroidSdCardExternalContentProvider";

    private string _rootDirectory = string.Empty;

    public string DisplayName => LanguageManager.Get(_typeName, 1);

    public string Description
    {
        get
        {
            InitializeFilesystemAccess();
            return _rootDirectory;
        }
    }

    public bool SupportsListing => true;

    public bool SupportsLinks => false;

    public bool RequiresLogin => false;

    public bool IsLoggedIn => true;

    public void Dispose()
    {
    }

    public void Login(CancellableProgress progress, Action success, Action<Exception> failure)
    {
        failure(new NotSupportedException());
    }

    public void Logout()
    {
        throw new NotSupportedException();
    }

    public void List(
        string path,
        CancellableProgress progress,
        Action<ExternalContentEntry> success,
        Action<Exception> failure
    )
    {
        ExternalContentEntry entry;
        Exception e;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                InitializeFilesystemAccess();
                var internalPath = ToInternalPath(path);
                entry = GetDirectoryEntry(internalPath, true);
                Dispatcher.Dispatch(delegate { success(entry); });
            }
            catch (Exception ex)
            {
                e = ex;
                Dispatcher.Dispatch(delegate { failure(e); });
            }
        });
    }

    public void Download(
        string path,
        CancellableProgress progress,
        Action<Stream> success,
        Action<Exception> failure
    )
    {
        FileStream stream;
        Exception e;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                InitializeFilesystemAccess();
                var path2 = ToInternalPath(path);
                stream = new FileStream(path2, FileMode.Open, FileAccess.Read, FileShare.Read);
                Dispatcher.Dispatch(delegate { success(stream); });
            }
            catch (Exception ex)
            {
                e = ex;
                Dispatcher.Dispatch(delegate { failure(e); });
            }
        });
    }

    public void Upload(
        string path,
        Stream stream,
        CancellableProgress progress,
        Action<string> success,
        Action<Exception> failure
    )
    {
        Exception e;
        ThreadPool.QueueUserWorkItem(delegate
        {
            try
            {
                InitializeFilesystemAccess();
                var uniquePath = GetUniquePath(ToInternalPath(path));
                var po = uniquePath;
                if (po.StartsWith("android:"))
                {
                    po = Storage.GetSystemPath(po);
                }

                var pp = Storage.GetDirectoryName(po);
                Directory.CreateDirectory(pp);
                using (var destination = new FileStream(po, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(destination);
                }

                Dispatcher.Dispatch(delegate { success(string.Empty); });
            }
            catch (Exception ex)
            {
                e = ex;
                Dispatcher.Dispatch(delegate { failure(e); });
            }
        });
    }

    public void Link(
        string path,
        CancellableProgress progress,
        Action<string> success,
        Action<Exception> failure
    )
    {
        failure(new NotSupportedException());
    }

    private ExternalContentEntry GetDirectoryEntry(string internalPath, bool scanContents)
    {
        var externalContentEntry = new ExternalContentEntry
        {
            Type = ExternalContentType.Directory,
            Path = ToExternalPath(internalPath),
            Time = new DateTime(1970, 1, 1)
        };

        if (!scanContents)
        {
            return externalContentEntry;
        }

        var directories = Directory.GetDirectories(internalPath);
        foreach (var internalPath2 in directories)
        {
            externalContentEntry.ChildEntries.Add(GetDirectoryEntry(internalPath2, false));
        }

        directories = Directory.GetFiles(internalPath);
        foreach (var text in directories)
        {
            var fileInfo = new FileInfo(text);
            var externalContentEntry2 = new ExternalContentEntry
            {
                Type = ExternalContentManager.ExtensionToType(Path.GetExtension(text)),
                Path = ToExternalPath(text),
                Size = fileInfo.Length,
                Time = fileInfo.CreationTime
            };
            externalContentEntry.ChildEntries.Add(externalContentEntry2);
        }

        return externalContentEntry;
    }

    private static string GetUniquePath(string path)
    {
        var num = 1;
        var text = path;
        while (File.Exists(text) && num < 1000)
        {
            var path2 = Path.GetFileNameWithoutExtension(path) + num + Path.GetExtension(path);
            text = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, path2);
            num++;
        }

        return text;
    }

    private string ToExternalPath(string internalPath)
    {
        return Path.GetFullPath(internalPath);
    }

    private string ToInternalPath(string externalPath)
    {
        return Path.Combine(_rootDirectory, externalPath);
    }

    private void InitializeFilesystemAccess()
    {
        Window.ActivityInstance.GetExternalFilesDir(null);
        _rootDirectory = RunPath.ExternalPath;
        if (!Storage.DirectoryExists(_rootDirectory))
        {
            Storage.CreateDirectory(_rootDirectory);
        }
    }
}

#endif
