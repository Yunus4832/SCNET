using System.Text.Json;
using System.Xml.Linq;

using Engine.Audio;
using Engine.Graphics;
using Engine.Media;

using Game.ContentReaders;

namespace Game.ModManager.Changes;

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

public static class ContentManager
{
    private static readonly Dictionary<string, ContentInfo> _resources = new();

    internal static readonly Dictionary<string, IContentReader> readerList = new();

    private static readonly Dictionary<string, List<object>> _caches = new();

    private static readonly Lock _syncObj = new();

    public static void Initialize()
    {
        readerList.Clear();
        _resources.Clear();
        _caches.Clear();
        Display.DeviceReset += DisplayDeviceReset;
    }

    public static T Get<T>(string name, string suffix = "") where T : class
    {
        return (T)Get(typeof(T), name, suffix);
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

            var contents = new List<ContentInfo>();
            string nameWithSuffix;
            if (string.IsNullOrEmpty(suffix))
            {
                foreach (var s in reader.DefaultSuffix)
                {
                    nameWithSuffix = name + "." + s;
                    if (_resources.TryGetValue(nameWithSuffix, out var contentInfo))
                    {
                        contents.Add(contentInfo);
                    }
                }
            }
            else
            {
                nameWithSuffix = name + suffix;
                if (_resources.TryGetValue(nameWithSuffix, out var contentInfo))
                {
                    contents.Add(contentInfo);
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
            "Engine.Media.StreamingSource" => SoundData.Stream(stream),
            "Engine.Audio.SoundBuffer" => SoundBuffer.Load(stream),
            "Engine.Graphics.Texture2D" => Texture2D.Load(stream),
            "System.String" => new StreamReader(stream).ReadToEnd(),
            "Engine.Media.Image" => Image.Load(stream),
            "Game.ObjModel" => ObjModelReader.Load(stream),
            "System.Xml.Linq.XElement" => XElement.Load(stream),
            "Engine.Graphics.Model" => Model.Load(stream, true),
            "Game.MtllibStruct" => MtllibStruct.Load(stream),
            _ => throw new InvalidOperationException("Unknown type stream")
        };
    }

    public static void Add(ContentInfo contentInfo)
    {
        lock (_syncObj)
        {
            if (!_resources.TryGetValue(contentInfo.AbsolutePath, out var info))
            {
                _resources.Add(contentInfo.AbsolutePath, contentInfo);
            }
            else
            {
                _resources[contentInfo.AbsolutePath] = contentInfo;
            }
        }
    }

    /// <summary>
    /// 可能需要带上文件后缀，即获取名字+获取的后缀
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
        return _resources.ContainsKey(key);
    }

    public static bool IsContent(object content)
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

    public static ReadOnlyList<ContentInfo> List()
    {
        return new ReadOnlyList<ContentInfo>(_resources.Values.ToDynamicArray());
    }

    public static ReadOnlyList<ContentInfo> List(string directory)
    {
        if (!directory.EndsWith("/"))
        {
            directory += "/";
        }

        var contents = _resources.Values.Where(content => content.ContentPath.StartsWith(directory)).ToList();
        return new ReadOnlyList<ContentInfo>(contents);
    }
}
