using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using static Game.Screens.NetPlayScreen;

namespace Game.ModManager;

public static class ModsManager
{
    public const string ApiVersion = "1.42";

    public const string ScVersion = "2.4.40.6";

    public const int ApiV = 3;

    public static readonly string ExternalPath = RunPath.ExternalPath;

    public static readonly string ConfigPath = RunPath.ConfigPath;

    private static readonly string _gameDataPath = RunPath.GameDataPath;

    public static readonly string ScreenCapturePath = $"{ExternalPath}/ScreenCapture";

    public static readonly string ModsPath = $"{ExternalPath}/NetMods";

    public static readonly string WorldsDirectoryName = $"{_gameDataPath}/Worlds";

    public static readonly string UserDataPath = $"{_gameDataPath}/UserId.dat";

    public static readonly string CharacterSkinsDirectoryName = $"{_gameDataPath}/CharacterSkins";

    public static readonly string FurniturePacksDirectoryName = $"{_gameDataPath}/FurniturePacks";

    public static readonly string BlockTexturesDirectoryName = $"{_gameDataPath}/TexturePacks";

    public static readonly string CommunityContentCachePath = $"{_gameDataPath}/CommunityContentCache.xml";

    public static readonly string ModCachePath = $"{_gameDataPath}/ModsCache";

    public static readonly string LogPath = $"{ExternalPath}/Logs";

    public static readonly string ModsSettingPath = "config:ModSettings.xml";

    public static readonly string SettingPath = "config:Settings.xml";

    public static readonly bool IsAndroid = OperatingSystem.IsAndroid();

    private static ModEntity _survivalCraftModEntity = null!;

    internal static bool configLoaded;

    public static readonly List<Connect> SaveConnects = [];

    public static readonly List<Connect> CollectConnects = [];

    public static readonly List<Connect> OnlineConnects = [];

    public class ModSettings
    {
        public string LanguageType = string.Empty;
    }

    public class ModHook(string name)
    {
        public readonly Dictionary<ModLoader, string> DisableReason = new();

        public string HookName = name;

        public readonly Dictionary<ModLoader, bool> Loaders = new();

        public void Add(ModLoader modLoader)
        {
            if (!Loaders.TryGetValue(modLoader, out _))
            {
                Loaders.Add(modLoader, true);
            }
        }

        public void Remove(ModLoader modLoader)
        {
            Loaders.Remove(modLoader, out _);
        }

        public void Disable(ModLoader from, ModLoader toDisable, string reason)
        {
            if (!Loaders.TryGetValue(toDisable, out _))
            {
                return;
            }

            if (!DisableReason.TryGetValue(from, out _))
            {
                DisableReason.Add(from, reason);
            }
        }
    }

    private static bool _allowContinue = true;

    public static readonly Dictionary<string, string> Configs = new();

    public static readonly List<ModEntity> ModListAll = [];

    public static readonly List<ModEntity> ModList = [];

    public static readonly List<ModLoader> ModLoaders = [];

    public static readonly List<ModInfo> DisabledMods = [];

    public static readonly Dictionary<string, ModHook> ModHooks = new();

    public static readonly Dictionary<string, Assembly> Dlls = new();

    public static bool GetModEntity(string packageName, out ModEntity? modEntity)
    {
        modEntity = ModList.Find(px => px.ModInfo.PackageName == packageName);
        return modEntity != null;
    }

    public static bool GetAllowContinue()
    {
        return _allowContinue;
    }

    internal static void Reboot()
    {
        SettingsManager.SaveSettings();
        SettingsManager.LoadSettings();
        foreach (var mod in ModList)
        {
            mod.Dispose();
        }

        ScreensManager.SwitchScreen("Loading");
    }

    /// <summary>
    /// 执行Hook
    /// </summary>
    /// <param name="hookName"></param>
    /// <param name="action"></param>
    public static void HookAction(string hookName, Func<ModLoader, bool> action)
    {
        if (ModHooks.TryGetValue(hookName, out var modHook))
        {
            foreach (var modLoader in modHook.Loaders.Keys)
            {
                if (action.Invoke(modLoader))
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 注册Hook
    /// </summary>
    /// <param name="hookName"></param>
    /// <param name="modLoader"></param>
    public static void RegisterHook(string hookName, ModLoader modLoader)
    {
        if (!ModHooks.TryGetValue(hookName, out var modHook))
        {
            modHook = new ModHook(hookName);
            ModHooks.Add(hookName, modHook);
        }

        modHook.Add(modLoader);
    }

    public static void DisableHook(ModLoader from, string hookName, string packageName, string reason)
    {
        var modEntity = ModList.Find(p => p.ModInfo.PackageName == packageName);
        if (!ModHooks.TryGetValue(hookName, out var modHook))
        {
            return;
        }

        var modLoader = modEntity!.Loader;
        if (modLoader != null)
        {
            modHook.Disable(from, modLoader, reason);
        }
    }

    public static T GetInPakOrStorageFile<T>(string filepath, string suffix = ".txt") where T : class
    {
        return ContentManager.Get<T>(filepath, suffix);
    }

    public static T? DeserializeJson<T>(string text) where T : class
    {
        return JsonSerializer.Deserialize<T>(text);
    }

    public static void SaveModSettings(XElement xElement)
    {
        foreach (var modEntity in ModList)
        {
            modEntity.SaveSettings(xElement);
        }

        using var stream = Storage.OpenFile(ModsSettingPath, OpenFileMode.Create);
        XmlUtils.SaveXmlToStream(xElement, stream, null, true);
    }

    public static void SaveSettings(XElement xElement)
    {
        var element = new XElement("Configs");
        foreach (var c in Configs)
        {
            element.SetAttributeValue(c.Key, c.Value);
        }

        xElement.Add(element);

        var saveConnects = new XElement("SaveConnects");
        foreach (var connect in SaveConnects)
        {
            var lc = XmlUtils.AddElement(saveConnects, "SaveConnects");
            lc.SetAttributeValue("IP", connect.IP);
            lc.SetAttributeValue("Name", connect.Name);
            lc.SetAttributeValue("Pass", connect.SavedPassword);
        }

        xElement.Add(saveConnects);

        var collectConnects = new XElement("CollectConnects");
        foreach (var connect in CollectConnects)
        {
            var lc = XmlUtils.AddElement(collectConnects, "CollectConnects");
            lc.SetAttributeValue("IP", connect.IP);
            lc.SetAttributeValue("Name", connect.Name);
            lc.SetAttributeValue("Pass", connect.SavedPassword);
        }

        xElement.Add(collectConnects);
    }

    public static void LoadSettings(XElement xElement)
    {
        SaveConnects.Clear();
        CollectConnects.Clear();
        var config = xElement.Element("Configs");
        if (config != null)
        {
            foreach (var c in config.Attributes())
            {
                if (!Configs.ContainsKey(c.Name.LocalName))
                {
                    SetConfig(c.Name.LocalName, c.Value);
                }
            }

            configLoaded = true;
        }

        var saveConnects = xElement.Element("SaveConnects");
        if (saveConnects != null)
        {
            foreach (var elem in saveConnects.Elements())
            {
                var ip = elem.Attribute("IP")?.Value ?? throw new InvalidOperationException("IP is null");
                var name = elem.Attribute("Name")?.Value ?? string.Empty;
                var passWd = elem.Attribute("Pass")?.Value ?? string.Empty;
                SaveConnects.Add(new Connect { IP = ip, Name = name, SavedPassword = passWd });
            }
        }

        var collectConnects = xElement.Element("CollectConnects");
        if (collectConnects == null)
        {
            return;
        }

        foreach (var elem in collectConnects.Elements())
        {
            var ip = elem.Attribute("IP")?.Value ?? throw new InvalidOperationException("IP is null");
            var name = elem.Attribute("Name")?.Value ?? string.Empty;
            var passWd = elem.Attribute("Pass")?.Value ?? string.Empty;
            CollectConnects.Add(new Connect { IP = ip, Name = name, SavedPassword = passWd });
        }
    }

    public static void LoadModSettings(XElement xElement)
    {
        foreach (var modEntity in ModList)
        {
            modEntity.LoadSettings(xElement);
        }
    }

    public static void SetConfig(string key, string value)
    {
        Configs[key] = value;
    }

    public static string ImportMod(string name, Stream stream)
    {
        if (!Storage.DirectoryExists(ModCachePath))
        {
            Storage.CreateDirectory(ModCachePath);
        }

        var path = Storage.CombinePaths(ModCachePath, name + ".scmod");
        var num = 1;
        while (Storage.FileExists(path))
        {
            path = Storage.CombinePaths(ModCachePath, name + "(" + num + ").scmod");
            num++;
        }

        using (var fileStream = Storage.OpenFile(path, OpenFileMode.CreateOrOpen))
        {
            stream.CopyTo(fileStream);
        }

        DialogsManager.ShowDialog(null, new MessageDialog("Mod下载成功", "请到Mod管理器中进行手动安装，是否跳转", "前往", "返回",
            delegate(MessageDialogButton result)
            {
                if (result == MessageDialogButton.Button1)
                {
                    ScreensManager.SwitchScreen("ModsManageContent");
                }
            }));
        return "Mod下载成功";
    }

    public static void ModListAllDo(Action<ModEntity> entity)
    {
        foreach (var item in ModList)
        {
            entity?.Invoke(item);
        }
    }

    public static void Initialize()
    {
        if (!Storage.DirectoryExists(ModsPath))
        {
            Storage.CreateDirectory(ModsPath);
        }

        ModHooks.Clear();
        ModListAll.Clear();
        ModLoaders.Clear();
        _survivalCraftModEntity = new SurvivalCraftModEntity();
        ModEntity fastDebug = new FastDebugModEntity();
        ModListAll.Add(_survivalCraftModEntity);
        ModListAll.Add(fastDebug);
        GetScmods(ModsPath);
        var toDisable = new List<ModInfo>();
        toDisable.AddRange(DisabledMods);
        DisabledMods.Clear();
        var toRemove = new List<ModEntity>();
        //读取 Scmod 文件到 ModListAll 列表
        foreach (var modEntity1 in ModListAll)
        {
            var modInfo = modEntity1.ModInfo;

            var disabledMod = toDisable.Find(l => l.PackageName == modInfo.PackageName);
            if (disabledMod != null && disabledMod.PackageName != _survivalCraftModEntity.ModInfo.PackageName &&
                disabledMod.PackageName != fastDebug.ModInfo.PackageName)
            {
                toDisable.Add(modInfo);
                toRemove.Add(modEntity1);
                continue;
            }

            var modEntities = ModListAll.FindAll(px => px.ModInfo.PackageName == modInfo.PackageName);
            if (modEntities.Count > 1)
            {
                AddException(new Exception($"Multiple installed [{modInfo.PackageName}]"));
            }
        }

        DisabledMods.Clear();
        foreach (var item in toDisable)
        {
            DisabledMods.Add(item);
        }

        foreach (var item in toRemove)
        {
            ModListAll.Remove(item);
        }

        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainAssemblyResolve;
    }

    private static Assembly? CurrentDomainAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        try
        {
            return Dlls.TryGetValue(args.Name, out var dll) ? dll : null;
        }
        catch (Exception e)
        {
            Log.Information($"加载程序集{args.Name}失败:{e.Message}");
            throw;
        }
    }

    public static void AddException(Exception e, bool allowContinue = false)
    {
        LoadingScreen.Error(e.Message);
        _allowContinue = !SettingsManager.DisplayLog || allowContinue;
    }

    /// <summary>
    /// 获取所有文件
    /// </summary>
    /// <param name="path"></param>
    public static void GetScmods(string path)
    {
        foreach (var item in Storage.ListFileNames(path))
        {
            var ms = Storage.GetExtension(item);
            var ks = Storage.CombinePaths(path, item);
            using var stream = Storage.OpenFile(ks, OpenFileMode.Read);
            try
            {
                if (ms == ".scmod")
                {
                    var modEntity = new ModEntity(ks, ZipArchive.ZipArchive.Open(stream, true));
                    if (string.IsNullOrEmpty(modEntity.ModInfo.PackageName))
                    {
                        continue;
                    }

                    ModListAll.Add(modEntity);
                }
            }
            catch (Exception e)
            {
                AddException(e);
            }
        }

        foreach (var dir in Storage.ListDirectoryNames(path))
        {
            GetScmods(Storage.CombinePaths(path, dir));
        }
    }

    public static string StreamToString(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        return new StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    /// 将 Stream 转成 byte[]
    /// </summary>
    public static byte[] StreamToBytes(Stream stream)
    {
        var bytes = new byte[stream.Length];
        stream.Seek(0, SeekOrigin.Begin);
        stream.ReadExactly(bytes, 0, bytes.Length);
        // 设置当前流的位置为流的开始
        return bytes;
    }

    public static string GetMd5(string input)
    {
        return GetMd5(Encoding.Default.GetBytes(input));
    }

    public static string GetMd5(byte[] input)
    {
        var data = MD5.HashData(input);
        var sBuilder = new StringBuilder();
        foreach (var item in data)
        {
            sBuilder.Append(item.ToString("x2"));
        }

        return sBuilder.ToString();
    }

    public static bool FindElement(XElement? xElement, Func<XElement, bool> func, out XElement? elementOut)
    {
        if (xElement is null)
        {
            elementOut = null;
            return false;
        }

        foreach (var element in xElement.Elements())
        {
            if (func(element))
            {
                elementOut = element;
                return true;
            }

            if (FindElement(element, func, out var element1))
            {
                elementOut = element1;
                return true;
            }
        }

        elementOut = null;
        return false;
    }

    public static bool FindElementByGuid(XElement xElement, string guid, out XElement? elementout)
    {
        foreach (var element in xElement.Elements())
        {
            if (element.Attributes()
                .Any(xAttribute => xAttribute.Name.ToString() == "Guid" && xAttribute.Value == guid))
            {
                elementout = element;
                return true;
            }

            if (!FindElementByGuid(element, guid, out var element1))
            {
                continue;
            }

            elementout = element1;
            return true;
        }

        elementout = null;
        return false;
    }

    public static bool HasAttribute(XElement element, Func<string, bool> func, out XAttribute? xAttributeout)
    {
        foreach (var xAttribute in element.Attributes())
        {
            if (func(xAttribute.Name.LocalName))
            {
                xAttributeout = xAttribute;
                return true;
            }
        }

        xAttributeout = null;
        return false;
    }

    public static void CombineClo(XElement? xElement, Stream cloorcr)
    {
        if (xElement is null)
        {
            return;
        }

        var mergeXml = XmlUtils.LoadXmlFromStream(cloorcr, Encoding.UTF8, true);
        foreach (var element in mergeXml.Elements())
        {
            if (HasAttribute(element, name => name.StartsWith("new-"), out var attribute))
            {
                if (HasAttribute(element, name => name == "Index", out var xAttribute))
                {
                    if (FindElement(xElement, _ => element.Attribute("Index")!.Value == xAttribute!.Value,
                            out var element1))
                    {
                        var px = attribute!.Name.ToString()
                            .Split(["new-"], StringSplitOptions.RemoveEmptyEntries);
                        if (px.Length == 1)
                        {
                            element1!.SetAttributeValue(px[0], attribute.Value);
                        }
                    }
                }
            }
            else if (HasAttribute(element, name => name.StartsWith("r-"), out _))
            {
                if (HasAttribute(element, name => name == "Index", out var xAttribute))
                {
                    if (FindElement(xElement, _ => element.Attribute("Index")!.Value == xAttribute!.Value,
                            out var element1))
                    {
                        element1!.Remove();
                        element.Remove();
                    }
                }
            }

            xElement.Add(mergeXml);
        }
    }

    public static void CombineCr(XElement xElement, Stream cloorcr)
    {
        var mergeXml = XmlUtils.LoadXmlFromStream(cloorcr, Encoding.UTF8, true);
        CombineCrLogic(xElement, mergeXml);
    }

    public static void CombineCrLogic(XElement xElement, XElement needCombine)
    {
        foreach (var element in needCombine.Elements())
        {
            if (HasAttribute(element, name => name == "Result", out _))
            {
                if (HasAttribute(element, name => name.StartsWith("new-"), out var attribute))
                {
                    var px = attribute!.Name.ToString()
                        .Split(["new-"], StringSplitOptions.RemoveEmptyEntries);

                    if (FindElement(xElement, ele =>
                            {
                                //原始标签
                                foreach (var xAttribute in element.Attributes()) //待修改的标签
                                {
                                    if (xAttribute.Name == attribute.Name)
                                    {
                                        continue;
                                    }

                                    if (!HasAttribute(ele, tname => tname == xAttribute.Name, out _))
                                    {
                                        return false;
                                    }
                                }

                                return true;
                            },
                            out var element1))
                    {
                        if (px.Length == 1)
                        {
                            element1!.SetAttributeValue(px[0], attribute.Value);
                            element1.SetValue(element.Value);
                        }
                    }
                }
                else if (HasAttribute(element, name => name.StartsWith("r-"), out var attribute1))
                {
                    if (FindElement(xElement, ele =>
                        {
                            //原始标签
                            foreach (var xAttribute in element.Attributes()) //待修改的标签
                            {
                                if (xAttribute.Name == attribute1!.Name)
                                {
                                    continue;
                                }

                                if (!HasAttribute(ele, tname => tname == xAttribute.Name, out _))
                                {
                                    return false;
                                }
                            }

                            return true;
                        }, out var element1))
                    {
                        element1!.Remove();
                        element.Remove();
                    }
                }
                else
                {
                    xElement.Add(element);
                }
            }

            CombineCrLogic(xElement, element);
        }
    }

    public static void Modify(XElement source, XElement change)
    {
        if (FindElement(source, item => item.Name.LocalName == change.Name.LocalName &&
                                        item.Attribute("Guid") != null &&
                                        change.Attribute("Guid") != null &&
                                        item.Attribute("Guid")?.Value == change.Attribute("Guid")?.Value,
                out var xElement1))
        {
            foreach (var xElement in change.Elements())
            {
                Modify(xElement1!, xElement);
            }
        }
        else
        {
            if (change.Name.LocalName.StartsWith("Parameter") || change.Name.LocalName == "MemberComponentTemplate")
            {
                if (FindElement(source, item => item.Name.LocalName == change.Name.LocalName &&
                                                item.Attribute("Name")?.Name == change.Attribute("Name")?.Name,
                        out var x))
                {
                    Log.Warning($"重复的参数{x!.Name.LocalName}:{x.Attribute("Name")?.Value}设置");
                }
                else
                {
                    source.Add(change);
                }
            }
            else
            {
                source.Add(change);
            }
        }
    }

    public static void CombineDataBase(XElement? dataBaseXml, Stream xdb)
    {
        var mergeXml = XmlUtils.LoadXmlFromStream(xdb, Encoding.UTF8, true);
        var dataObjects = dataBaseXml?.Element("DatabaseObjects");
        if (dataObjects is null)
        {
            return;
        }

        foreach (var element in mergeXml.Elements())
        {
            //处理修改
            if (HasAttribute(element, str => str.Contains("new-"), out var attribute))
            {
                if (HasAttribute(element, str => str == "Guid", out var attribute1))
                {
                    if (FindElementByGuid(dataObjects, attribute1!.Value, out var xElement))
                    {
                        var px = attribute!.Name.ToString().Split(["new-"], StringSplitOptions.RemoveEmptyEntries);
                        if (px.Length == 1)
                        {
                            xElement!.SetAttributeValue(px[0], attribute.Value);
                        }
                    }
                }
            }

            Modify(dataObjects, element);
        }
    }

}
