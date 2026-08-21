using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using Game.ContentProviders;

namespace Game.Managers;

public static class MotdManager
{
    private static readonly string _defaultMotdUpdateUrl =
        SchubExternalContentProvider.GetPath("/com/motd?v={0}&l={1}");

    private static readonly string _defaultMotdUpdateCheckUrl =
        SchubExternalContentProvider.GetPath("/com/motd?v={0}&cmd=version_check&platform={1}&apiv={2}&l={3}");

    public static Bulletin BulletinDefault = Bulletin.Default;

    public static bool CanShowBulletin { get; set; }

    private static bool _canDownloadMotd = true;

    private static bool _forceChecked;

    public static readonly List<FilterMod> FilterModAll = [];

    public static JsonObject? UpdateResult;

    private static bool _isAdmin;

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

    private static void ClientCheck()
    {
        try
        {
            var header = new Dictionary<string, string>
            {
                { "Content-Type", "application/x-www-form-urlencoded" }
            };
            var dictionary = new Dictionary<string, string>
            {
                { "version", VersionsManager.ProtocolVersion }
            };
            WebManager.Post(
                SchubExternalContentProvider.GetPath("/com/api/zh/forceCheckCode"),
                new Dictionary<string, string>(),
                header,
                WebManager.UrlParametersToStream(dictionary),
                new CancellableProgress(),
                delegate(byte[] data)
                {
                    _forceChecked = true;
                    if (WebManager.JsonFromBytes(data) is not JsonObject result)
                    {
                        return;
                    }

                    if (result.TryGetPropertyValue("code", out var codeNode) && codeNode?.ToString() != "200")
                    {
                        return;
                    }

                    if (!result.TryGetPropertyValue("msg", out var msgNode) || msgNode?.ToString() != "Bomb")
                    {
                        return;
                    }

                    Log.Warning("当前版本已过期，请等待新版本");
                    Window.Close();
                },
                delegate { }
            );
        }
        catch
        {
            // ignored
        }
    }

    public static void Update()
    {
        if (!_forceChecked && Time.PeriodicEvent(30, 15))
        {
            ClientCheck();
        }

        if (_canDownloadMotd)
        {
            DownloadMotd();
            CommunityContentManager.IsAdmin(new CancellableProgress(), delegate(bool isAdmin) { _isAdmin = isAdmin; },
                delegate { });
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

    private static void SaveBulletin(
        string dataString,
        CancellableProgress progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        if (!WebManager.IsInternetConnectionAvailable())
        {
            failure(new InvalidOperationException("Internet connection is unavailable."));
            return;
        }

        var header = new Dictionary<string, string>
        {
            { "Content-Type", "application/x-www-form-urlencoded" }
        };
        var dictionary = new Dictionary<string, string>
        {
            { "Operater", SettingsManager.Current.CommunityAccessToken },
            { "Content", dataString }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/setnotice"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            success,
            failure
        );
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
                delegate(LabelWidget titleLabel, LabelWidget contentLabel)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new TextBoxDialog(
                            "请输入标题",
                            titleLabel.Text,
                            1024,
                            delegate(string inputTitle)
                            {
                                DialogsManager.ShowDialog(
                                    null,
                                    new TextBoxDialog(
                                        "请输入内容",
                                        contentLabel.Text.Replace("\n", "[n]"),
                                        8192,
                                        delegate(string inputContent)
                                        {
                                            if (string.IsNullOrEmpty(inputTitle) ||
                                                string.IsNullOrEmpty(inputContent))
                                            {
                                                return;
                                            }

                                            titleLabel.Text = inputTitle;
                                            contentLabel.Text = inputContent.Replace("[n]", "\n");
                                            if (IsCnLanguageType())
                                            {
                                                BulletinDefault.Title = titleLabel.Text;
                                                BulletinDefault.Content = contentLabel.Text;
                                            }
                                            else
                                            {
                                                BulletinDefault.EnTitle = titleLabel.Text;
                                                BulletinDefault.EnContent = contentLabel.Text;
                                            }

                                            var languageType =
                                                !AppConfigStore.Values.TryGetValue("Language", out var config)
                                                    ? "zh-CN"
                                                    : config;
                                            BulletinDefault.Time = languageType + "$" + DateTime.Now;
                                        },
                                        delegate(TextBoxWidget textBox)
                                        {
                                            textBox.Text = textBox.Text.Replace("\n", "[n]");
                                        }
                                    )
                                );
                            }
                        )
                    );
                },
                delegate(LabelWidget titleLabel, LabelWidget contentLabel)
                {
                    var num = SettingsManager.Current.MotdLastDownloadedData.IndexOf("<Motd2", StringComparison.Ordinal);
                    var num2 = SettingsManager.Current.MotdLastDownloadedData.IndexOf("</Motd2>", StringComparison.Ordinal) + 8;
                    var xElement =
                        XmlUtils.LoadXmlFromString(SettingsManager.Current.MotdLastDownloadedData.Substring(num, num2 - num),
                            true);
                    _ = !AppConfigStore.Values.TryGetValue("Language", out var config)
                        ? "zh-CN"
                        : config;
                    foreach (var item in xElement.Elements())
                    {
                        if (item.Name.LocalName != "Bulletin")
                        {
                            continue;
                        }

                        if (IsCnLanguageType())
                        {
                            item.Attribute("Title")?.Value = titleLabel.Text;
                            item.Element("Content")?.Value = contentLabel.Text;
                        }
                        else
                        {
                            item.Attribute("EnTitle")?.Value = titleLabel.Text;
                            item.Element("EnContent")?.Value = contentLabel.Text;
                        }

                        item.Attribute("Time")?.Value = DateTime.Now.ToString(CultureInfo.InvariantCulture);
                        break;
                    }

                    var newDownloadedData = SettingsManager.Current.MotdLastDownloadedData.Substring(0, num);
                    newDownloadedData += xElement.ToString();
                    newDownloadedData += SettingsManager.Current.MotdLastDownloadedData.Substring(num2);
                    var busyDialog = new CancellableBusyDialog("操作等待中", false);
                    DialogsManager.ShowDialog(null, busyDialog);
                    SaveBulletin(
                        newDownloadedData,
                        busyDialog.Progress,
                        delegate(byte[] data)
                        {
                            DialogsManager.HideDialog(busyDialog);
                            if (WebManager.JsonFromBytes(data) is not JsonObject result)
                            {
                                return;
                            }

                            var msg = result[0]?.ToString() == "200" ? "公告已更新,建议重启游戏检查效果" : result[1]?.ToString();
                            msg ??= string.Empty;
                            if (result[0]?.ToString() == "200")
                            {
                                SettingsManager.Current.MotdLastDownloadedData = newDownloadedData;
                            }

                            DialogsManager.ShowDialog(
                                null,
                                new MessageDialog(
                                    "操作成功",
                                    msg,
                                    LanguageManager.Ok
                                )
                            );
                        },
                        delegate(Exception e)
                        {
                            DialogsManager.HideDialog(busyDialog);
                            Log.Error("SaveBulletin:" + e.Message);
                        });
                });
            CommunityContentManager.IsAdmin(
                new CancellableProgress(),
                delegate(bool isAdmin) { _isAdmin = isAdmin; },
                delegate { }
            );
            bulletinDialog.EditButton.IsVisible = _isAdmin;
            bulletinDialog.UpdateButton.IsVisible = _isAdmin;
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
        string.IsNullOrWhiteSpace(SettingsManager.Current.MotdUpdateUrl)
            ? _defaultMotdUpdateUrl
            : SettingsManager.Current.MotdUpdateUrl;

    private static string GetMotdUpdateCheckUrl() =>
        string.IsNullOrWhiteSpace(SettingsManager.Current.MotdUpdateCheckUrl)
            ? _defaultMotdUpdateCheckUrl
            : SettingsManager.Current.MotdUpdateCheckUrl;

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
