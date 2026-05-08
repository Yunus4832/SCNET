using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.ModManager;

public static class LanguageControl
{
    public static JsonObject KeyWords = new();

    public static string Ok = string.Empty;

    public static string Cancel = string.Empty;

    public static string None = string.Empty;

    public static string Nothing = string.Empty;

    public static string Error = string.Empty;

    public static string On = string.Empty;

    public static string Off = string.Empty;

    public static string Disable = string.Empty;

    public static string Enable = string.Empty;

    public static string Warning = string.Empty;

    public static string Back = string.Empty;

    public static string Allowed = string.Empty;

    public static string NAllowed = string.Empty;

    public static string Unknown = string.Empty;

    public static string Yes = string.Empty;

    public static string No = string.Empty;

    public static string Unavailable = string.Empty;

    public static string Exists = string.Empty;

    public static string Success = string.Empty;

    public static string Delete = string.Empty;

    public static readonly List<string> LanguageTypes = [];

    public static void Initialize(string languageType)
    {
        KeyWords.Clear();
        ModsManager.SetConfig("Language", languageType);
    }

    /// <summary>
    /// 新的 loadJson 方法，接受语言类型参数
    /// </summary>
    public static void LoadJson(Stream stream, string id)
    {
        try
        {
            var txt = new StreamReader(stream).ReadToEnd();
            if (txt.Length <= 0)
            {
                return;
            }

            var newJsonObject = JsonSerializer.Deserialize<JsonObject>(txt);
            MergeJsonObject(KeyWords, newJsonObject);
        }
        catch (Exception ex)
        {
            Log.Error($"加载语言文件时出错: {ex.Message}");
        }
    }

    public static void RefreshCommonWords()
    {
        Ok = Get("Usual", "ok");
        Cancel = Get("Usual", "cancel");
        None = Get("Usual", "none");
        Nothing = Get("Usual", "nothing");
        Error = Get("Usual", "error");
        On = Get("Usual", "on");
        Off = Get("Usual", "off");
        Disable = Get("Usual", "disable");
        Enable = Get("Usual", "enable");
        Warning = Get("Usual", "warning");
        Back = Get("Usual", "back");
        Allowed = Get("Usual", "allowed");
        NAllowed = Get("Usual", "not allowed");
        Unknown = Get("Usual", "unknown");
        Yes = Get("Usual", "yes");
        No = Get("Usual", "no");
        Unavailable = Get("Usual", "Unavailable");
        Exists = Get("Usual", "exist");
        Success = Get("Usual", "success");
        Delete = Get("Usual", "delete");
    }

    private static void MergeJsonObject(JsonObject? oldObject, JsonObject? newObject)
    {
        if (oldObject == null || newObject == null)
        {
            return;
        }

        foreach (var newChild in newObject)
        {
            if (oldObject.ContainsKey(newChild.Key))
            {
                var oldChild = oldObject[newChild.Key]?.DeepClone();
                if (oldChild is JsonObject oldJsonObject && newChild.Value is JsonObject newJsonObject)
                {
                    MergeJsonObject(oldJsonObject, newJsonObject);
                }
                else if (oldChild is JsonArray oldJsonArray && newChild.Value is JsonArray newJsonArray)
                {
                    MergeJsonArray(oldJsonArray, newJsonArray);
                }
                else
                {
                    oldObject[newChild.Key] = newChild.Value?.DeepClone();
                }
            }
            else
            {
                oldObject[newChild.Key] = newChild.Value?.DeepClone();
            }
        }
    }

    private static void MergeJsonArray(JsonArray oldArray, JsonArray newArray)
    {
        if (newArray.Count >= oldArray.Count)
        {
            oldArray.Clear();
            foreach (var item in newArray)
            {
                oldArray.Add(item?.DeepClone());
            }
        }
        else
        {
            for (var i = 0; i < newArray.Count; i++)
            {
                oldArray[i] = newArray[i]?.DeepClone();
            }
        }
    }

    // 获取当前语言的标识符
    public static string LName()
    {
        return ModsManager.Configs["Language"];
    }

    public static string Get(string className, int key)
    {
        //获得键值
        return Get(className, key.ToString());
    }

    public static string AutoGet(object className, int key)
    {
        //获得键值
        return Get(className.GetType().Name, key.ToString());
    }

    public static string GetWorldPalette(int index)
    {
        return Get("WorldPalette", "Colors", index.ToString());
    }

    public static string Get(params string[] key)
    {
        return Get(out _, key);
    }

    private static string Get(out bool r, params string[] key)
    {
        //获得键值
        r = false;
        var obj = KeyWords;
        JsonArray? arr = null;
        foreach (var item in key)
        {
            var flag = false;
            if (arr != null)
            {
                int.TryParse(item, out var p);
                var obj2 = arr[p];
                if (obj2 is JsonObject jo)
                {
                    obj = jo;
                    arr = null;
                    flag = true;
                }
                else if (obj2 is JsonArray ja)
                {
                    obj = null;
                    arr = ja;
                    flag = true;
                }
                else
                {
                    r = true;
                    return obj2?.ToString() ?? string.Empty;
                }
            }
            else
            {
                if (obj!.ContainsKey(item))
                {
                    var obj2 = obj[item];
                    if (obj2 is JsonObject jo)
                    {
                        obj = jo;
                        arr = null;
                        flag = true;
                    }
                    else if (obj2 is JsonArray ja)
                    {
                        obj = null;
                        arr = ja;
                        flag = true;
                    }
                    else
                    {
                        r = true;
                        return obj2?.ToString() ?? string.Empty;
                    }
                }
            }

            if (!flag)
            {
                return item;
            }
        }

        return key.Aggregate("", (current, s) => current + s + ":");
    }

    public static string GetBlock(string blockName, string prop)
    {
        return TryGetBlock(blockName, prop, out var result) ? result! : string.Empty;
    }

    public static bool TryGetBlock(string blockName, string prop, out string? result)
    {
        var nm = blockName.Split([':'], StringSplitOptions.None);
        result = Get(out var r, "Blocks", nm.Length < 2 ? blockName + ":0" : blockName, prop);
        if (!r)
        {
            result = Get(out r, "Blocks", nm[0] + ":0", prop);
        }

        return r;
    }

    public static string GetContentWidgets(string name, string prop)
    {
        return Get("ContentWidgets", name, prop);
    }

    public static string GetContentWidgets(string name, int pos)
    {
        return Get("ContentWidgets", name, pos.ToString());
    }

    public static string GetDatabase(string name, string prop)
    {
        return Get("Database", name, prop);
    }

    public static string GetFireworks(string name, string prop)
    {
        return Get("FireworksBlock", name, prop);
    }
}
