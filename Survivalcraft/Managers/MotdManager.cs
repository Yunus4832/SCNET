using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

namespace Game.Managers;

public static class MotdManager
{
    public static Bulletin BulletinDefault = Bulletin.Default;

    public static bool CanShowBulletin { get; set; }

    private static bool _canDownloadMotd = true;

    public static readonly List<FilterMod> FilterModAll = [];

    public static JsonObject? UpdateResult;

    public static Message MessageOfTheDay
    {
        get;
        set
        {
            field = value;
            MessageOfTheDayUpdated?.Invoke();
        }
    } = Message.Default;

    public static event Action? MessageOfTheDayUpdated;

    public static void ForceRedownload()
    {
        SettingsManager.Current.MotdLastUpdateTime = DateTime.MinValue;
    }

    public static void UpdateVersion()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.Current.MotdUpdateCheckUrl))
        {
            return;
        }

        var url = string.Format(
            GetMotdUpdateCheckUrl(),
            VersionsManager.Version,
            PlatformManager.Platform,
            ModPlatformInfo.ApiVersion,
            LanguageManager.LName()
        );
        WebManager.Get(
            url,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new CancellableProgress(),
            data => { UpdateResult = JsonSerializer.Deserialize<JsonObject>(Encoding.UTF8.GetString(data)); },
            ex => { Log.Error("Failed processing Update check. Reason: " + ex.Message); }
        );
    }

    private static void DownloadMotd()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.Current.MotdUpdateUrl))
        {
            return;
        }

        var url = GetMotdUrl();
        WebManager.Get(
            url,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new CancellableProgress(),
            delegate(byte[] result)
            {
                try
                {
                    var motdLastDownloadedData = UnpackMotd(result);
                    MessageOfTheDay = Message.Default;
                    SettingsManager.Current.MotdLastDownloadedData = motdLastDownloadedData;
                    Log.Information("Downloaded MOTD");
                }
                catch (Exception ex)
                {
                    Log.Error("Failed processing MOTD string. Reason: " + ex.Message);
                }
            },
            delegate(Exception error) { Log.Error("Failed downloading MOTD. Reason: {0}", error.Message); }
        );
    }

    public static void Update()
    {
        if (_canDownloadMotd)
        {
            DownloadMotd();
            _canDownloadMotd = false;
        }

        if (string.IsNullOrEmpty(SettingsManager.Current.MotdLastDownloadedData))
        {
            return;
        }

        MessageOfTheDay = ParseMotd(SettingsManager.Current.MotdLastDownloadedData);
        if (MessageOfTheDay.Lines.Count == 0)
        {
            SettingsManager.Current.MotdLastDownloadedData = string.Empty;
        }

        if (!string.IsNullOrEmpty(BulletinDefault.Content) ||
            SettingsManager.Current.BulletinTime == BulletinDefault.Time)
        {
            return;
        }

        if (IsCnLanguageType() && BulletinDefault.Title.ToLower() != "null" ||
            !IsCnLanguageType() && BulletinDefault.EnTitle.ToLower() != "null")
        {
            CanShowBulletin = true;
        }
    }

    private static string UnpackMotd(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return new StreamReader(stream).ReadToEnd();
    }

    private static Message ParseMotd(string dataString)
    {
        try
        {
            var num = dataString.IndexOf("<Motd", StringComparison.Ordinal);
            if (num < 0)
            {
                throw new InvalidOperationException("Invalid MOTD data string.");
            }

            var num2 = dataString.IndexOf("</Motd>", StringComparison.Ordinal);
            if (num2 >= 0 && num2 > num)
            {
                num2 += 7;
            }

            var xElement = XmlUtils.LoadXmlFromString(dataString.Substring(num, num2 - num), true);
            SettingsManager.Current.MotdUpdatePeriodHours = XmlUtils.GetAttributeValue(xElement, "UpdatePeriodHours", 24);
            SettingsManager.Current.MotdUpdateUrl =
                XmlUtils.GetAttributeValue(xElement, "UpdateUrl", GetMotdUpdateUrl());
            var message = new Message();
            foreach (var item2 in xElement.Elements())
            {
                if (!Widget.IsNodeIncludedOnCurrentPlatform(item2))
                {
                    continue;
                }

                var item = new Line
                {
                    Time = XmlUtils.GetAttributeValue<float>(item2, "Time"),
                    Node = item2.Elements().FirstOrDefault(),
                    Text = item2.Value
                };
                message.Lines.Add(item);
            }

            LoadBulletin(dataString);
            LoadFilterMods(dataString);
            return message;
        }
        catch (Exception ex)
        {
            Log.Warning("Failed extracting MOTD string. Reason: " + ex.Message);
        }

        return Message.Default;
    }

    private static void LoadBulletin(string dataString)
    {
        var num = dataString.IndexOf("<Motd2", StringComparison.Ordinal);
        if (num < 0)
        {
            throw new InvalidOperationException("Invalid MOTD2 data string.");
        }

        var num2 = dataString.IndexOf("</Motd2>", StringComparison.Ordinal);
        if (num2 >= 0 && num2 > num)
        {
            num2 += 8;
        }

        var xElement = XmlUtils.LoadXmlFromString(dataString.Substring(num, num2 - num), true);
        var languageType = !AppConfigStore.Values.TryGetValue("Language", out var config) ? "zh-CN" : config;
        foreach (var item in xElement.Elements())
        {
            if (item.Name.LocalName != "Bulletin")
            {
                continue;
            }

            BulletinDefault = new Bulletin
            {
                Title = item.Attribute("Title")?.Value ?? string.Empty,
                EnTitle = item.Attribute("EnTitle")?.Value ?? string.Empty,
                Time = languageType + "$" + item.Attribute("Time")?.Value,
                Content = item.Element("Content")?.Value ?? string.Empty,
                EnContent = item.Element("EnContent")?.Value ?? string.Empty,
            };
            break;
        }
    }

    private static void LoadFilterMods(string dataString)
    {
        var num = dataString.IndexOf("<Motd3", StringComparison.Ordinal);
        if (num < 0)
        {
            throw new InvalidOperationException("Invalid MOTD3 data string.");
        }

        var num2 = dataString.IndexOf("</Motd3>", StringComparison.Ordinal);
        if (num2 >= 0 && num2 > num)
        {
            num2 += 8;
        }

        var xElement = XmlUtils.LoadXmlFromString(dataString.Substring(num, num2 - num), true);
        FilterModAll.Clear();
        foreach (var item in xElement.Elements())
        {
            if (item.Name.LocalName != "FilterMod")
            {
                continue;
            }

            var filterMod = new FilterMod
            {
                Name = item.Attribute("Name")?.Value ?? string.Empty,
                PackageName = item.Attribute("PackageName")?.Value ?? string.Empty,
                Version = item.Attribute("Version")?.Value ?? string.Empty,
                FilterApiVersion = item.Attribute("FilterAPIVersion")?.Value ?? string.Empty,
                Explanation = item.Value
            };
            FilterModAll.Add(filterMod);
        }
    }

    public static void ShowBulletin()
    {
        try
        {
            var time = BulletinDefault.Time.Contains('$')
                ? BulletinDefault.Time.Split(['$'], StringSplitOptions.RemoveEmptyEntries)[1]
                : string.Empty;
            if (!string.IsNullOrEmpty(time))
            {
                time = (IsCnLanguageType() ? "公告发布时间: " : "Time: ") + time;
            }

            var title = IsCnLanguageType() ? BulletinDefault.Title : BulletinDefault.EnTitle;
            var content = IsCnLanguageType() ? BulletinDefault.Content : BulletinDefault.EnContent;
            var bulletinDialog = new BulletinDialog(title, content, time,
                delegate { SettingsManager.Current.BulletinTime = BulletinDefault.Time; },
                delegate { },
                delegate { });
            DialogsManager.ShowDialog(null, bulletinDialog);
            CanShowBulletin = false;
        }
        catch (Exception ex)
        {
            Log.Warning("Failed ShowBulletin. Reason: " + ex.Message);
        }
    }

    private static bool IsCnLanguageType()
    {
        var languageType = !AppConfigStore.Values.TryGetValue("Language", out var config) ? "zh-CN" : config;
        return languageType == "zh-CN";
    }

    private static string GetMotdUrl()
    {
        var languageType = !AppConfigStore.Values.TryGetValue("Language", out var config) ? "zh-CN" : config;
        return string.Format(GetMotdUpdateUrl(), VersionsManager.Version, languageType);
    }

    private static string GetMotdUpdateUrl() =>
        SettingsManager.Current.MotdUpdateUrl;

    private static string GetMotdUpdateCheckUrl() =>
        SettingsManager.Current.MotdUpdateCheckUrl;

    public class Message
    {
        public static readonly Message Default = new();

        public readonly List<Line> Lines = [];
    }

    public class Line
    {
        public XElement? Node;

        public string Text = string.Empty;

        public float Time;
    }

    public class Bulletin
    {
        public static readonly Bulletin Default = new();

        public string Content = string.Empty;

        public string EnContent = string.Empty;

        public string EnTitle = string.Empty;

        public string Time = string.Empty;

        public string Title = string.Empty;
    }

    public class FilterMod
    {
        public string Explanation = string.Empty;

        public string FilterApiVersion = string.Empty;

        public string Name = string.Empty;

        public string PackageName = string.Empty;

        public string Version = string.Empty;
    }
}
