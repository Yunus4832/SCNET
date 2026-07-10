using Newtonsoft.Json;

namespace Game;

public static class GameUpdateHelper
{
    private const string _typeName = nameof(GameUpdateHelper);

    private static readonly string _checkUpdateUrl =
        $"http://schelper.trk34.top:34340/com/updatehelper?version={VersionsManager.ProtocolVersion}";

    public static void CheckGameUpdate()
    {
        if (SettingsManager.Current.RejectedUpdateCount >= 3)
        {
            return;
        }
#pragma warning disable CS4014
        // 不等待这个方法完成
        CheckForUpdatesAsync();
#pragma warning restore CS4014
    }

    private static async Task CheckForUpdatesAsync()
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetAsync(_checkUpdateUrl);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<UpdateInformation>(responseBody);
            if (result is { NoRelease: true })
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        "错误",
                        "本版本未开放",
                        LanguageManager.Ok,
                        string.Empty,
                        delegate { Window.Close(); }
                    )
                );
                return;
            }

            if (result is { NeedUpdate: true })
            {
                var dialog = new MessageDialog(
                    LanguageManager.Get(_typeName, 1),
                    result.UpdateMessage,
                    LanguageManager.Get("Usual", "yes"),
                    LanguageManager.Get("Usual", "no"),
                    new Vector2(-1f),
                    (button, self) =>
                    {
                        if (button == MessageDialogButton.Button1)
                        {
                            WebBrowserManager.LaunchBrowser(result.UpdateUrl);
                            DialogsManager.HideDialog(self);
                        }
                        else
                        {
                            DialogsManager.HideDialog(self);
                            SettingsManager.Current.RejectedUpdateCount++;
                        }
                    }
                )
                {
                    AutoHide = false
                };
                DialogsManager.ShowDialog(null, dialog);
            }
        }
        catch
        {
            Log.Information("未获取到更新信息");
        }
    }
}

public class UpdateInformation
{
    public bool NeedUpdate { get; set; }

    public bool NoRelease { get; set; }

    public string UpdateMessage { get; set; } = string.Empty;

    public string UpdateUrl { get; set; } = string.Empty;
}
