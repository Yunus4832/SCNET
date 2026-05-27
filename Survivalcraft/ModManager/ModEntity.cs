using System.Text;
using System.Xml.Linq;

using Engine.Graphics;

using Game.ContentReaders;
using Game.Network.Packages;
using Game.Network.Serialization;
using Game.ZipArchive;

namespace Game.ModManager;

public class ModEntity
{
    public readonly List<Block> Blocks = [];

    public Texture2D Icon
    {
        get => field is not null ? field : throw new InvalidOperationException("Icon is not initialized");
        set;
    } = null!;

    public bool IsDependencyChecked;

    protected ZipArchive.ZipArchive ModArchive
    {
        get => field is not null ? field : throw new InvalidOperationException("ModArchive is not initialized");
        set;
    } = null!;

    public readonly string ModFilePath = string.Empty;

    private readonly Dictionary<string, ZipArchiveEntry> _modFiles = new();

    public ModInfo ModInfo
    {
        get => field is not null ? field : throw new InvalidOperationException("ModInfo is not initialized");
        set;
    } = null!;

    public string ResourcesMd5 = string.Empty;

    public virtual bool IsSystemMod => false;

    public ModLoader? Loader { get; set; }

    public ModEntity()
    {
    }

    public ModEntity(ZipArchive.ZipArchive zipArchive)
    {
        ModFilePath = ModsManager.ModsPath;
        ModArchive = zipArchive;
        InitResources();
    }

    public ModEntity(string fileName, ZipArchive.ZipArchive zipArchive)
    {
        ModFilePath = fileName;
        ModArchive = zipArchive;
        InitResources();
    }

    public virtual void LoadIcon(Stream stream)
    {
#if SERVER
        return;
#else
        Icon = Texture2D.Load(stream);
        stream.Close();
#endif
    }

    /// <summary>
    /// 获取指定后缀文件列表，带.
    /// </summary>
    /// <param name="extension"></param>
    /// <param name="action">参数1文件名参数，2打开的文件流</param>
    public virtual void GetFiles(string extension, Action<string, Stream> action)
    {
        //将每个zip里面的文件读进内存中
        foreach (var zipArchiveEntry in ModArchive.ReadCentralDir())
        {
            if (Storage.GetExtension(zipArchiveEntry.FilenameInZip) == extension)
            {
                var stream = new MemoryStream();
                ModArchive.ExtractFile(zipArchiveEntry, stream);
                stream.Position = 0L;
                try
                {
                    action.Invoke(zipArchiveEntry.FilenameInZip, stream);
                }
                catch (Exception e)
                {
                    Log.Error($"获取文件[{zipArchiveEntry.FilenameInZip}]失败：{e.Message}");
                }
                finally
                {
                    stream.Dispose();
                }
            }
        }
    }

    /// <summary>
    /// 获取指定文件
    /// </summary>
    /// <param name="filename"></param>
    /// <param name="stream">参数1打开的文件流</param>
    /// <returns></returns>
    protected virtual bool GetFile(string filename, Action<Stream> stream)
    {
        if (string.Equals(filename, "icon.png", StringComparison.OrdinalIgnoreCase))
        {
#if SERVER
            return false;
#endif
        }

        if (!_modFiles.TryGetValue(filename, out var entry))
        {
            return false;
        }

        using var ms = new MemoryStream();
        ModArchive.ExtractFile(entry, ms);
        ms.Position = 0L;
        try
        {
            stream?.Invoke(ms);
        }
        catch (Exception e)
        {
            LoadingScreen.Error($"[{ModInfo.Name}]获取文件[{filename}]失败：" + e.Message);
        }

        return false;
    }

    protected virtual bool GetAssetsFile(string filename, Action<Stream> stream)
    {
        return GetFile("Assets/" + filename, stream);
    }

    /// <summary>
    /// 初始化语言包
    /// </summary>
    public virtual void LoadLanguage()
    {
        LoadingScreen.Info($"[{ModInfo.Name}]加载Lang语言目录");
        GetAssetsFile($"Lang/{ModsManager.Configs["Language"]}.json",
            stream => { LanguageControl.LoadJson(stream, ModsManager.Configs["Language"]); });
    }

    /// <summary>
    /// Mod初始化
    /// </summary>
    public virtual void ModInitialize()
    {
        LoadingScreen.Info($"[{ModInfo.Name}]初始化Mod方法");
        Loader?.ModInitialize();
    }

    /// <summary>
    /// 初始化Pak资源
    /// </summary>
    protected virtual void InitResources()
    {
        _modFiles.Clear();
        var entries = ModArchive.ReadCentralDir();
        foreach (var zipArchiveEntry in entries)
        {
            if (zipArchiveEntry.FileSize > 0)
            {
                _modFiles.Add(zipArchiveEntry.FilenameInZip, zipArchiveEntry);
            }
        }

        GetFile("modinfo.json",
            stream =>
            {
                ModInfo = ModsManager.DeserializeJson<ModInfo>(ModsManager.StreamToString(stream))
                          ?? throw new InvalidOperationException("Deserialize ModFile error");
            });
#if !SERVER
        GetFile("icon.png", LoadIcon);
#endif
        foreach (var c in _modFiles)
        {
            var zipArchiveEntry = c.Value;
            var filename = zipArchiveEntry.FilenameInZip;
            if (!zipArchiveEntry.IsFilenameUtf8)
            {
                var gbk = Encoding.GetEncoding("GBK");
                var utf = Encoding.UTF8;
                var p = utf.GetString(Encoding.Convert(gbk, utf, gbk.GetBytes(zipArchiveEntry.FilenameInZip)));
                ModsManager.AddException(
                    new Exception($"[{ModInfo.Name}]文件名[{zipArchiveEntry.FilenameInZip}]编码不是UTF-8，请进行修正，GBK编码为[{p}]"));
            }

            if (filename.StartsWith("Assets/"))
            {
                var memoryStream = new MemoryStream();
                var contentInfo = new ContentInfo(filename.Substring(7));
                ModArchive.ExtractFile(zipArchiveEntry, memoryStream);
                contentInfo.SetContentStream(memoryStream);
                ContentManager.Add(contentInfo);
            }
        }

        LoadingScreen.Info($"[{ModInfo.Name}]加载资源文件数:{_modFiles.Count}");
    }

    /// <summary>
    /// 初始化BlocksData资源
    /// </summary>
    public virtual void LoadBlocksData()
    {
        LoadingScreen.Info($"[{ModInfo.Name}]加载.csv方块数据文件");
        GetFiles(".csv", (_, stream) => { BlocksManager.LoadBlocksData(ModsManager.StreamToString(stream)); });
    }

    /// <summary>
    /// 初始化Database数据
    /// </summary>
    /// <param name="xElement"></param>
    public virtual void LoadXdb(ref XElement? xElement)
    {
        var element = xElement;
        LoadingScreen.Info($"[{ModInfo.Name}]加载.xdb数据库文件");
        GetFiles(".xdb", (_, stream) => { ModsManager.CombineDataBase(element, stream); });
        Loader?.OnXdbLoad(xElement);
    }

    /// <summary>
    /// 初始化Clothing数据
    /// </summary>
    /// <param name="block"></param>
    /// <param name="xElement"></param>
    public virtual void LoadClo(ClothingBlock block, ref XElement? xElement)
    {
        var element = xElement;
        LoadingScreen.Info($"[{ModInfo.Name}]加载.clo衣物数据文件");
        GetFiles(".clo", (_, stream) => { ModsManager.CombineClo(element, stream); });
    }

    /// <summary>
    /// 初始化CraftingRecipe
    /// </summary>
    /// <param name="xElement"></param>
    public virtual void LoadCr(ref XElement xElement)
    {
        var element = xElement;
        LoadingScreen.Info($"[{ModInfo.Name}]加载.cr合成谱文件");
        GetFiles(".cr", (_, stream) => { ModsManager.CombineCr(element, stream); });
    }

    /// <summary>
    /// 加载mod程序集
    /// </summary>
    public virtual void LoadDll()
    {
        LoadingScreen.Info($"[{ModInfo.Name}]加载.dll程序集文件");
        GetFiles(".dll", (_, stream) => { LoadDllLogic(stream); });
    }

    protected void LoadDllLogic(Stream stream)
    {
        var assembly = Assembly.Load(ModsManager.StreamToBytes(stream));
        ModsManager.Dlls.Add(assembly.FullName!, assembly);
        var blockTypes = new List<Type>();
        var types = assembly.GetTypes();
        foreach (var type in types)
        {
            if (type.IsSubclassOf(typeof(ModLoader)) && !type.IsAbstract)
            {
                if (Activator.CreateInstance(type) is ModLoader modLoader)
                {
                    modLoader.Entity = this;
                    Loader = modLoader;
                    modLoader.ModInitialize();
                    ModsManager.ModLoaders.Add(modLoader);
                }
            }

            if (type.IsSubclassOf(typeof(IContentReader)) && !type.IsAbstract)
            {
                if (Activator.CreateInstance(type) is IContentReader reader)
                {
                    ContentManager.readerList.TryAdd(reader.Type, reader);
                }
            }

            if (type.IsSubclassOf(typeof(IPackage)) && !type.IsAbstract)
            {
                if (Activator.CreateInstance(type) is IPackage pack)
                {
                    PackageManager.RegisterPackage(pack);
                }
            }

            if (type.IsSubclassOf(typeof(Block)) && !type.IsAbstract)
            {
                blockTypes.Add(type);
            }
        }

        foreach (var type in blockTypes)
        {
            var fieldInfo = type.GetRuntimeFields()
                .FirstOrDefault(p => p is { Name: "Index", IsPublic: true, IsStatic: true });
            if (fieldInfo == null || fieldInfo.FieldType != typeof(int))
            {
                LoadingScreen.Warning($"Block type \"{type.FullName}\" does not have static field Index of type int.");
            }
            else
            {
                var staticIndex = (int)fieldInfo.GetValue(null)!;
                var block = (Block?)Activator.CreateInstance(type.GetTypeInfo().AsType());
                if (block == null)
                {
                    continue;
                }

                block.BlockIndex = staticIndex;
                Blocks.Add(block);
            }
        }
    }

    /// <summary>
    /// 检查依赖项
    /// </summary>
    public virtual void CheckDependencies(List<ModEntity> modEntities)
    {
        LoadingScreen.Info($"[{ModInfo.Name}]检查依赖项");
        foreach (var name in ModInfo.Dependencies)
        {
            var dn = "";
            var dnVersion = new Version();
            if (name.Contains(':'))
            {
                var temp = name.Split([':']);
                if (temp.Length == 2)
                {
                    dn = temp[0];
                    dnVersion = new Version(temp[1]);
                }
            }
            else
            {
                dn = name;
            }

            var entity = ModsManager.ModListAll.Find(px =>
                px.ModInfo.PackageName == dn && new Version(px.ModInfo.Version) == dnVersion);
            if (entity != null)
            {
                //依赖项最先被加载
                if (!entity.IsDependencyChecked)
                {
                    entity.CheckDependencies(modEntities);
                }
            }
            else
            {
                ModsManager.AddException(new Exception($"[{ModInfo.Name}]缺少依赖项{name}"));
                return;
            }
        }

        IsDependencyChecked = true;
        modEntities.Add(this);
    }

    /// <summary>
    /// 保存设置
    /// </summary>
    /// <param name="xElement"></param>
    public virtual void SaveSettings(XElement xElement)
    {
        Loader?.SaveSettings(xElement);
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    /// <param name="xElement"></param>
    public virtual void LoadSettings(XElement xElement)
    {
        Loader?.LoadSettings(xElement);
    }

    /// <summary>
    /// BlocksManager初始化完毕
    /// </summary>
    public virtual void OnBlocksInitialized()
    {
        Loader?.BlocksInitialized();
    }

    //释放资源
    public virtual void Dispose()
    {
        try
        {
            Loader?.ModDispose();
        }
        catch
        {
            // ignored
        }

        ModArchive.ZipFileStream.Close();
    }

    public override bool Equals(object? obj)
    {
        if (obj is ModEntity px)
        {
            return px.ModInfo.PackageName == ModInfo.PackageName && px.ModInfo.Version == ModInfo.Version;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return ModInfo.GetHashCode();
    }
}
