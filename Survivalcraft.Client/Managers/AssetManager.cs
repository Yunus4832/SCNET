using System.Text.Json;
using System.Xml.Linq;

using Engine.Audio;
using Engine.Graphics;
using Engine.Media.Audio;
using Engine.Media.Images;

using Game.AssetReaders;

namespace Game.Managers;

public static class AssetManager
{
    private static readonly Dictionary<string, AssetInfo> _resources = new();

    private static readonly Dictionary<string, ReadOnlyList<AssetInfo>> _directoryLists = new(StringComparer.Ordinal);

    internal static readonly Dictionary<string, AssetReader> readerList = new();

    private static readonly Dictionary<string, List<object>> _caches = new();

    private static readonly Dictionary<string, ModId> _resourceOwners = new();

    private static readonly Lock _syncObj = new();

    private static bool _deviceResetSubscribed;

    public static void Initialize()
    {
        lock (_syncObj)
        {
            DisposeAllCaches();
            foreach (var resource in _resources.Values)
            {
                resource.Dispose();
            }

            readerList.Clear();
            _resources.Clear();
            _directoryLists.Clear();
            _resourceOwners.Clear();
            if (!_deviceResetSubscribed)
            {
                Display.DeviceReset += DisplayDeviceReset;
                _deviceResetSubscribed = true;
            }
        }
    }

    public static void RegisterReader(AssetReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        lock (_syncObj)
        {
            readerList.TryAdd(reader.Type, reader);
        }
    }

    public static T Get<T>(string name, string suffix = "") where T : class
    {
        return (T)Get(typeof(T), name, suffix);
    }

    internal static Stream OpenRelativeResource(AssetInfo owner, string resource)
    {
        if (Uri.TryCreate(resource, UriKind.Absolute, out _) || resource.Contains('\\'))
        {
            throw new InvalidDataException("Model dependencies must use relative asset paths.");
        }

        var directory = owner.AbsolutePath.LastIndexOf('/');
        var components = new List<string>();
        var combined = (directory < 0 ? string.Empty : owner.AbsolutePath[..(directory + 1)]) + resource;
        foreach (var component in combined.Split('/'))
        {
            if (component is "" or ".")
            {
                continue;
            }

            if (component == "..")
            {
                if (components.Count == 0)
                {
                    throw new InvalidDataException("Model dependency escapes the content root.");
                }

                components.RemoveAt(components.Count - 1);
            }
            else
            {
                components.Add(component);
            }
        }

        lock (_syncObj)
        {
            var path = string.Join('/', components);
            return _resources.TryGetValue(path, out var content)
                ? content.Duplicate()
                : throw new FileNotFoundException($"Model dependency '{path}' was not found.", path);
        }
    }

    public static Func<Stream> CreateStreamFactory(string name, params string[] suffixes)
    {
        lock (_syncObj)
        {
            foreach (var suffix in suffixes)
            {
                if (_resources.TryGetValue(name + "." + suffix, out var resource))
                {
                    return resource.CreateStreamFactory();
                }
            }

            throw new FileNotFoundException($"Audio resource was not found: {name}");
        }
    }

    public static object Get(Type type, string name, string suffix = "")
    {
        lock (_syncObj)
        {
            object? obj = null;
            var key = string.IsNullOrEmpty(suffix) ? name : name + "." + suffix;
            if (type == typeof(Subtexture))
            {
                return TextureAtlasManager.GetSubtexture(name);
            }

            if (_caches.TryGetValue(key, out var cacheList))
            {
                obj = cacheList.Find(f => f.GetType() == type);
            }

            if (obj != null)
            {
                return obj;
            }

            if (type.FullName is null)
            {
                throw new InvalidOperationException("Type.FullName is null");
            }

            if (!readerList.TryGetValue(type.FullName, out var reader))
            {
                throw new InvalidOperationException("未找到对应资源的读取器");
            }

            var contents = new List<AssetInfo>();
            string nameWithSuffix;
            if (string.IsNullOrEmpty(suffix))
            {
                foreach (var s in reader.DefaultSuffix)
                {
                    nameWithSuffix = name + "." + s;
                    if (_resources.TryGetValue(nameWithSuffix, out var assetInfo))
                    {
                        contents.Add(assetInfo);
                    }
                }
            }
            else
            {
                nameWithSuffix = name + suffix;
                if (_resources.TryGetValue(nameWithSuffix, out var assetInfo))
                {
                    contents.Add(assetInfo);
                }
            }

            if (contents.Count == 0)
                //没有找到对应资源?
            {
                throw new Exception("未能找到资源[" + name + "][" + type.FullName + "]");
            }

            obj = reader.Get(contents.ToArray());

            if (cacheList == null)
            {
                cacheList = [];
                _caches.Add(key, cacheList);
            }

            cacheList.Add(obj);
            return obj;
        }
    }

    public static object StreamConvertType(Type type, Stream stream)
    {
        return type.FullName switch
        {
            "JsonObject" => JsonSerializer.Deserialize<object>(new StreamReader(stream).ReadToEnd()) ??
                            throw new InvalidOperationException("JsonString deserialize failed"),
            "Engine.Media.Audio.StreamingSource" => SoundData.Stream(stream),
            "Engine.Audio.SoundBuffer" => SoundBuffer.Load(stream),
            "Engine.Graphics.Texture2D" => Texture2D.Load(stream),
            "System.String" => new StreamReader(stream).ReadToEnd(),
            "Engine.Media.Images.Image" => Image.Load(stream),
            "System.Xml.Linq.XElement" => XElement.Load(stream),
            "Engine.Graphics.Model" => Model.Load(stream),
            _ => throw new InvalidOperationException("Unknown type stream")
        };
    }

    public static void Add(AssetInfo assetInfo)
    {
        lock (_syncObj)
        {
            if (_resourceOwners.TryGetValue(assetInfo.AbsolutePath, out var owner))
            {
                throw new InvalidOperationException(
                    $"Asset {assetInfo.AbsolutePath} is owned by mod {owner} and cannot be replaced by legacy content."
                );
            }

            Replace(assetInfo);
        }
    }

    public static void AddOwned(ModId owner, AssetInfo assetInfo)
    {
        lock (_syncObj)
        {
            if (_resources.ContainsKey(assetInfo.AbsolutePath) &&
                !_resourceOwners.TryGetValue(assetInfo.AbsolutePath, out var existingOwner))
            {
                throw new InvalidOperationException(
                    $"Asset {assetInfo.AbsolutePath} already exists without a mod owner."
                );
            }

            if (_resourceOwners.TryGetValue(assetInfo.AbsolutePath, out existingOwner) && existingOwner != owner)
            {
                throw new InvalidOperationException(
                    $"Asset {assetInfo.AbsolutePath} is already owned by mod {existingOwner}."
                );
            }

            Replace(assetInfo);
            _resourceOwners[assetInfo.AbsolutePath] = owner;
        }
    }

    public static bool ContainsOwned(ModId owner, string path)
    {
        lock (_syncObj)
        {
            return _resourceOwners.TryGetValue(path, out var existingOwner) && existingOwner == owner;
        }
    }

    public static void RemoveOwner(ModId owner)
    {
        var errors = new List<Exception>();
        lock (_syncObj)
        {
            var paths = _resourceOwners.Where(pair => pair.Value == owner).Select(pair => pair.Key).ToArray();
            foreach (var path in paths)
            {
                _resourceOwners.Remove(path);
                if (!_resources.Remove(path, out var resource)) { continue; }

                _directoryLists.Clear();

                try { Invalidate(resource.AssetPath); }
                catch (Exception error) { errors.Add(error); }

                try { resource.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            }
        }

        if (errors.Count > 0) { throw new AggregateException($"Failed to release content owned by {owner}.", errors); }
    }

    /// <summary>
    ///     可能需要带上文件后缀，即获取名字+获取的后缀
    /// </summary>
    public static void Dispose(string name)
    {
        lock (_syncObj)
        {
            if (!_caches.TryGetValue(name, out var list))
            {
                return;
            }

            var toRemove = new List<object>();
            foreach (var t in list)
            {
                if (t is IDisposable d)
                {
                    d.Dispose();
                }

                toRemove.Add(t);
            }

            foreach (var t in toRemove)
            {
                list.Remove(t);
            }
        }
    }

    public static bool ContainsKey(string key)
    {
        lock (_syncObj)
        {
            return _resources.ContainsKey(key);
        }
    }

    public static bool IsAsset(object content)
    {
        return _caches.Values.SelectMany(l => l).Any(d => d == content);
    }

    public static void DisplayDeviceReset()
    {
        foreach (var (key, value) in _caches)
        {
            for (var i = 0; i < value.Count; i++)
            {
                var item = value[i];
                if (item is Texture2D or Model or BitmapFont)
                {
                    value[i] = Get(item.GetType(), key);
                }
            }
        }
    }

    public static ReadOnlyList<AssetInfo> List()
    {
        return new ReadOnlyList<AssetInfo>(_resources.Values.ToDynamicArray());
    }

    private static void Replace(AssetInfo assetInfo)
    {
        if (_resources.TryGetValue(assetInfo.AbsolutePath, out var previous))
        {
            Invalidate(previous.AssetPath);
            previous.Dispose();
        }

        _resources[assetInfo.AbsolutePath] = assetInfo;
        _directoryLists.Clear();
    }

    private static void Invalidate(string contentPath)
    {
        DisposeCaches(
            _caches.Keys.Where(key => key.Equals(contentPath, StringComparison.Ordinal) ||
                                      key.StartsWith(contentPath + ".", StringComparison.Ordinal)
            ).ToArray()
        );
    }

    private static void DisposeCaches(IEnumerable<string> keys)
    {
        var errors = new List<Exception>();
        foreach (var key in keys)
        {
            if (!_caches.Remove(key, out var values)) { continue; }

            foreach (var value in values.OfType<IDisposable>())
            {
                try { value.Dispose(); }
                catch (Exception error) { errors.Add(error); }
            }

            values.Clear();
        }

        if (errors.Count > 0) { throw new AggregateException("Failed to release cached content.", errors); }
    }

    private static void DisposeAllCaches() => DisposeCaches(_caches.Keys.ToArray());

    public static ReadOnlyList<AssetInfo> List(string directory)
    {
        if (!directory.EndsWith('/'))
        {
            directory += "/";
        }

        lock (_syncObj)
        {
            if (!_directoryLists.TryGetValue(directory, out var contents))
            {
                contents = new ReadOnlyList<AssetInfo>(
                    _resources.Values
                        .Where(content => content.AssetPath.StartsWith(directory, StringComparison.Ordinal))
                        .ToList()
                );
                _directoryLists.Add(directory, contents);
            }

            return contents;
        }
    }
}
