using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.Localization;

public sealed class LanguageCatalog
{
    public JsonObject KeyWords = new();

    public string Ok = string.Empty;

    public string Cancel = string.Empty;

    public string None = string.Empty;

    public string Nothing = string.Empty;

    public string Error = string.Empty;

    public string On = string.Empty;

    public string Off = string.Empty;

    public string Disable = string.Empty;

    public string Enable = string.Empty;

    public string Warning = string.Empty;

    public string Info = string.Empty;

    public string Back = string.Empty;

    public string Allowed = string.Empty;

    public string NAllowed = string.Empty;

    public string Unknown = string.Empty;

    public string Yes = string.Empty;

    public string No = string.Empty;

    public string Unavailable = string.Empty;

    public string Exists = string.Empty;

    public string Success = string.Empty;

    public string Delete = string.Empty;

    public readonly List<string> LanguageTypes = [];

    private readonly Dictionary<string, string> _languageDisplayNames = new(StringComparer.OrdinalIgnoreCase);

    private string _currentLanguage = "zh-CN";

    private int _revision;

    public string CurrentLanguage => Volatile.Read(ref _currentLanguage);

    public int Revision => Volatile.Read(ref _revision);

    public void BeginInitialization()
    {
        KeyWords.Clear();
    }

    public void ClearLanguageDisplayNames()
    {
        _languageDisplayNames.Clear();
    }

    public void RegisterLanguageDisplayName(string languageType, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(languageType))
        {
            return;
        }

        _languageDisplayNames[languageType.Trim()] = string.IsNullOrWhiteSpace(displayName)
            ? languageType.Trim()
            : displayName.Trim();
    }

    public string GetLanguageDisplayName(string languageType)
    {
        return _languageDisplayNames.TryGetValue(languageType, out var displayName)
            ? displayName
            : languageType;
    }

    /// <summary>
    ///     新的 loadJson 方法，接受语言类型参数
    /// </summary>
    public void LoadJson(Stream stream, string id)
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

    public void RefreshCommonWords()
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
        Info = Get("Usual", "info");
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

    public void CompleteInitialization(string languageType)
    {
        Volatile.Write(ref _currentLanguage, languageType);
        Interlocked.Increment(ref _revision);
    }

    private void MergeJsonObject(JsonObject? oldObject, JsonObject? newObject)
    {
        if (oldObject == null || newObject == null)
        {
            return;
        }

        foreach (var newChild in newObject)
        {
            if (TryGetProperty(oldObject, newChild.Key, out var oldChild))
            {
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

    private bool TryGetProperty(JsonObject obj, string key, out JsonNode? value)
    {
        foreach (var pair in obj)
        {
            if (!string.Equals(pair.Key, key, StringComparison.Ordinal))
            {
                continue;
            }

            value = pair.Value;
            return true;
        }

        value = null;
        return false;
    }

    private void MergeJsonArray(JsonArray oldArray, JsonArray newArray)
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
    public string LName()
    {
        return CurrentLanguage;
    }

    public string Get(string className, int key)
    {
        //获得键值
        return Get(className, key.ToString());
    }

    public string AutoGet(object className, int key)
    {
        //获得键值
        return Get(className.GetType().Name, key.ToString());
    }

    public string GetWorldPalette(int index)
    {
        return Get("WorldPalette", "Colors", index.ToString());
    }

    public string Get(params string[] key)
    {
        return Get(out _, key);
    }

    public bool TryGet(out string value, params string[] key)
    {
        value = Get(out var found, key);
        return found;
    }

    private string Get(out bool r, params string[] key)
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
                if (!TryGetProperty(obj!, item, out var obj2))
                {
                    return item;
                }

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

            if (!flag)
            {
                return item;
            }
        }

        return key.Aggregate("", (current, s) => current + s + ":");
    }

    public string GetBlock(string blockName, string prop)
    {
        return TryGetBlock(blockName, prop, out var result) ? result! : string.Empty;
    }

    public bool TryGetBlock(string blockName, string prop, out string? result)
    {
        var nm = blockName.Split([':'], StringSplitOptions.None);
        result = Get(out var r, "Blocks", nm.Length < 2 ? blockName + ":0" : blockName, prop);
        if (!r)
        {
            result = Get(out r, "Blocks", nm[0] + ":0", prop);
        }

        return r;
    }

    public string GetContentWidgets(string name, string prop)
    {
        return Get("ContentWidgets", name, prop);
    }

    public string GetHelpTopic(string key, string property)
    {
        var value = Get("Help", key, property);
        if (!string.Equals(property, "value", StringComparison.Ordinal))
        {
            return value;
        }

        var text = string.Empty;
        var array = value.Split(["\n"], StringSplitOptions.None);
        text = array.Aggregate(text, (current, text2) => current + text2.Trim() + " ");
        text = text.Replace("\r", "");
        text = text.Replace("â€™", "'");
        text = text.Replace("\\n", "\n");
        return text;
    }

    public string GetContentWidgets(string name, int pos)
    {
        return Get("ContentWidgets", name, pos.ToString());
    }

    public string GetDatabase(string name, string prop)
    {
        return Get("Database", name, prop);
    }

    public string GetFireworks(string name, string prop)
    {
        return Get("FireworksBlock", name, prop);
    }
}
