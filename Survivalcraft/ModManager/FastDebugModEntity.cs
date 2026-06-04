using System.Xml.Linq;

namespace Game.ModManager;

public class FastDebugModEntity : ModEntity
{
    public readonly Dictionary<string, FileInfo> FModFiles = new();

    public FastDebugModEntity()
    {
        ModInfo = new ModInfo { Name = "[Debug]", PackageName = "debug" };
        InitResources();
    }

    public override bool IsSystemMod => true;

    protected override void InitResources()
    {
        ReadDirResources(ModsManager.ModsPath, "");
        if (!GetFile("modinfo.json", stream =>
            {
                ModInfo = ModsManager.DeserializeJson<ModInfo>(ModsManager.StreamToString(stream))
                          ?? throw new InvalidOperationException("ModInfo deserialized failed");
                ModInfo.Name = $"[Debug]{ModInfo.Name}";
            })
           )
        {
            ModInfo = new ModInfo
            {
                Name = "FastDebug", Version = "1.0.0", ApiVersion = ModsManager.ApiVersion, Author = "Mod",
                Description = "调试Mod插件", ScVersion = "2.4.40.6", PackageName = "com.fastdebug"
            };
        }

        if (RunMode.Value is RunModeType.Gui)
        {
            GetFile("icon.png", LoadIcon);
        }

        foreach (var c in FModFiles)
        {
            GetFile(c.Key, stream =>
            {
                var data = new byte[stream.Length];
                stream.ReadExactly(data, 0, data.Length);
                ResourcesMd5 += ModsManager.GetMd5(data);
            });
        }
    }

    private void ReadDirResources(string basepath, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = basepath;
        }

        foreach (var d in Storage.ListDirectoryNames(path))
        {
            ReadDirResources(basepath, path + "/" + d);
        }

        foreach (var f in Storage.ListFileNames(path))
        {
            var absolutePath = path + "/" + f;
            var filenameInZip = absolutePath[(basepath.Length + 1)..];
            if (filenameInZip.StartsWith("Assets/"))
            {
                var name = filenameInZip[7..];
                var contentInfo = new ContentInfo(name);
                var memoryStream = new MemoryStream();
                using var stream = Storage.OpenFile(absolutePath, OpenFileMode.Read);
                stream.CopyTo(memoryStream);
                contentInfo.SetContentStream(memoryStream);
                ContentManager.Add(contentInfo);
            }

            FModFiles.Add(filenameInZip, new FileInfo(Storage.GetSystemPath(absolutePath)));
        }
    }

    public override void LoadDll()
    {
        foreach (var c in Storage.ListFileNames(ModsManager.ModsPath))
        {
            if (c.EndsWith(".dll") && !(c.StartsWith("EntitySystem") || c.StartsWith("Engine") ||
                                        c.StartsWith("Survivalcraft") || c.StartsWith("OpenTK")))
            {
                LoadDllLogic(Storage.OpenFile(Storage.CombinePaths(ModsManager.ModsPath, c), OpenFileMode.Read));
                break;
            }
        }
    }

    public override void LoadClo(ClothingBlock block, ref XElement? xElement)
    {
        foreach (var c in Storage.ListFileNames(ModsManager.ModsPath))
        {
            if (c.EndsWith(".clo"))
            {
                ModsManager.CombineClo(xElement,
                    Storage.OpenFile(Storage.CombinePaths(ModsManager.ModsPath, c), OpenFileMode.Read));
            }
        }
    }

    public override void LoadCr(ref XElement xElement)
    {
        foreach (var c in Storage.ListFileNames(ModsManager.ModsPath))
        {
            if (c.EndsWith(".cr"))
            {
                ModsManager.CombineCr(xElement,
                    Storage.OpenFile(Storage.CombinePaths(ModsManager.ModsPath, c), OpenFileMode.Read));
            }
        }
    }

    public override void LoadLanguage()
    {
        // 定义语言文件所在的目录路径
        var path = Storage.CombinePaths(ModsManager.ModsPath, "Assets/Lang");

        // 检查目录是否存在
        if (Storage.DirectoryExists(path))
            // 遍历指定目录下的所有文件名
        {
            foreach (var c in Storage.ListFileNames(path))
            {
                // 获取目标语言文件名
                var fn = ModsManager.Configs["Language"] + ".json";

                // 检查文件名是否匹配
                if (c == fn)
                {
                    // 拼接完整的文件路径
                    var fpn = Storage.CombinePaths(path, c);

                    // 检查文件是否存在
                    if (Storage.FileExists(fpn))
                        // 加载语言文件
                        // LanguageControl.loadJson(Storage.OpenFile(fpn, OpenFileMode.Read));
                        // 加载语言文件，并传递语言代码作为第二个参数
                    {
                        LanguageControl.LoadJson(Storage.OpenFile(fpn, OpenFileMode.Read),
                            ModsManager.Configs["Language"]);
                    }
                }
            }
        }
    }

    public override void LoadBlocksData()
    {
        foreach (var c in Storage.ListFileNames(ModsManager.ModsPath))
        {
            if (c.EndsWith(".csv"))
            {
                BlocksManager.LoadBlocksData(ModsManager.StreamToString(
                    Storage.OpenFile(Storage.CombinePaths(ModsManager.ModsPath, c), OpenFileMode.Read)));
            }
        }
    }

    public override void LoadXdb(ref XElement? xElement)
    {
        foreach (var c in Storage.ListFileNames(ModsManager.ModsPath))
        {
            if (c.EndsWith(".xdb"))
            {
                ModsManager.CombineDataBase(xElement,
                    Storage.OpenFile(Storage.CombinePaths(ModsManager.ModsPath, c), OpenFileMode.Read));
            }
        }

        Loader?.OnXdbLoad(xElement);
    }

    /// <summary>
    /// 获取指定后缀文件列表，带.
    /// </summary>
    /// <param name="extension"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public override void GetFiles(string extension, Action<string, Stream> action)
    {
        foreach (var item in FModFiles)
        {
            if (item.Key.EndsWith(extension))
            {
                using Stream fs = item.Value.OpenRead();
                try
                {
                    action?.Invoke(item.Key, fs);
                }
                catch (Exception e)
                {
                    Log.Error($"GetFile {item.Key} Error:{e.Message}");
                }
            }
        }
    }

    protected override bool GetFile(string filename, Action<Stream> stream)
    {
        if (string.Equals(filename, "icon.png", StringComparison.OrdinalIgnoreCase))
        {
            if (RunMode.Value is RunModeType.HeadlessServer)
            {
                return false;
            }
        }

        if (!FModFiles.TryGetValue(filename, out var fileInfo))
        {
            return false;
        }

        using Stream fs = fileInfo.OpenRead();
        try
        {
            stream.Invoke(fs);
        }
        catch (Exception e)
        {
            Log.Error($"GetFile {filename} Error:{e.Message}");
        }

        return true;
    }

    protected override bool GetAssetsFile(string filename, Action<Stream> stream)
    {
        return GetFile("Assets/" + filename, stream);
    }
}
