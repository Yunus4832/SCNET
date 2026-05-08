using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Game.ContentProviders;

public class SchubExternalContentProvider : IExternalContentProvider
{
    private const string _appKey = "1uGA5aADX43p";

    private const string _appSecret = "9aux67wg5z";

    public const string RedirectUri = "https://m.schub.top";

    private LoginProcessData? _loginProcessData;

    public SchubExternalContentProvider()
    {
        Program.HandleUri += HandleUri;
        Window.Activated += WindowActivated;
    }

    public string DisplayName => "SC中文社区";

    public string Description => !IsLoggedIn ? "未登录" : "登陆";

    public bool SupportsListing => true;

    public bool SupportsLinks => true;

    public bool RequiresLogin => true;

    public bool IsLoggedIn => !string.IsNullOrEmpty(SettingsManager.CommunityAccessToken);

    public void Dispose()
    {
        Program.HandleUri -= HandleUri;
        Window.Activated -= WindowActivated;
    }

    public void Logout()
    {
        _loginProcessData = null;
        SettingsManager.CommunityAccessToken = string.Empty;
        SettingsManager.ScpboxUserInfo = string.Empty;
    }

    public void Login(
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        try
        {
            if (!WebManager.IsInternetConnectionAvailable())
            {
                throw new InvalidOperationException("网络连接错误");
            }

            Logout();
            LoginLaunchBrowser();
        }
        catch (Exception obj)
        {
            failure(obj);
        }
    }

    public void List(
        string path,
        CancellableProgress progress,
        Action<ExternalContentEntry> success,
        Action<Exception> failure
    )
    {
        try
        {
            VerifyLoggedIn();
            var dictionary = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + SettingsManager.CommunityAccessToken },
                { "Content-Type", "application/json" }
            };
            var jsonObject = new JsonObject
            {
                { "path", NormalizePath(path) },
                { "recursive", false },
                { "include_media_info", false },
                { "include_deleted", false },
                { "include_has_explicit_shared_members", false }
            };
            var data = new MemoryStream(Encoding.UTF8.GetBytes(jsonObject.ToString()));
            WebManager.Post(
                RedirectUri + "/com/files/list_folder",
                new Dictionary<string, string>(),
                dictionary,
                data,
                progress,
                delegate(byte[] result)
                {
                    try
                    {
                        success(JsonObjectToEntry((JsonObject?)WebManager.JsonFromBytes(result)));
                    }
                    catch (Exception obj2)
                    {
                        failure(obj2);
                    }
                },
                failure
            );
        }
        catch (Exception obj)
        {
            failure(obj);
        }
    }

    public void Download(
        string path,
        CancellableProgress progress,
        Action<Stream> success,
        Action<Exception> failure
    )
    {
        try
        {
            VerifyLoggedIn();
            var jsonObject = new JsonObject
            {
                { "path", NormalizePath(path) }
            };
            var dictionary = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + SettingsManager.CommunityAccessToken },
                { "Dropbox-API-Arg", jsonObject.ToString() }
            };
            WebManager.Get(
                RedirectUri + "/com/files/download",
                new Dictionary<string, string>(),
                dictionary,
                progress,
                delegate(byte[] result) { success(new MemoryStream(result)); },
                failure
            );
        }
        catch (Exception obj)
        {
            failure(obj);
        }
    }

    public void Upload(
        string path,
        Stream stream,
        CancellableProgress progress,
        Action<string> success,
        Action<Exception> failure
    )
    {
        try
        {
            VerifyLoggedIn();
            var jsonObject = new JsonObject
            {
                { "path", NormalizePath(path) },
                { "mode", "add" },
                { "autorename", true },
                { "mute", false }
            };
            var dictionary = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + SettingsManager.CommunityAccessToken },
                { "Content-Type", "application/octet-stream" },
                { "Dropbox-API-Arg", jsonObject.ToString() }
            };
            WebManager.Post(
                RedirectUri + "/com/files/upload",
                new Dictionary<string, string>(),
                dictionary,
                stream,
                progress,
                delegate { success(string.Empty); },
                failure
            );
        }
        catch (Exception obj)
        {
            failure(obj);
        }
    }

    public void Link(
        string path,
        CancellableProgress progress,
        Action<string> success,
        Action<Exception> failure
    )
    {
        try
        {
            VerifyLoggedIn();
            var dictionary = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + SettingsManager.CommunityAccessToken },
                { "Content-Type", "application/json" }
            };
            var jsonObject = new JsonObject
            {
                { "path", NormalizePath(path) },
                { "short_url", false }
            };
            var data = new MemoryStream(Encoding.UTF8.GetBytes(jsonObject.ToString()));
            WebManager.Post(
                RedirectUri + "/com/sharing/create_shared_link",
                new Dictionary<string, string>(),
                dictionary,
                data,
                progress,
                delegate(byte[] result)
                {
                    try
                    {
                        var jsonObject2 = (JsonObject?)WebManager.JsonFromBytes(result);
                        success(JsonObjectToLinkAddress(jsonObject2));
                    }
                    catch (Exception obj2)
                    {
                        failure(obj2);
                    }
                },
                failure
            );
        }
        catch (Exception obj)
        {
            failure(obj);
        }
    }

    public static string GetPath(string path)
    {
        return RedirectUri + path;
    }

    public void LoginLaunchBrowser()
    {
        try
        {
            var login = new LoginDialog();
            DialogsManager.ShowDialog(null, login);
        }
        catch (Exception error)
        {
            _loginProcessData?.Fail(this, error);
        }
    }

    public void WindowActivated()
    {
        if (_loginProcessData is not { IsTokenFlow: false })
        {
            return;
        }

        var loginProcessData = _loginProcessData;
        _loginProcessData = null;
        var dialog = new TextBoxDialog(
            "输入用户登录Token:",
            "",
            256,
            delegate(string s)
            {
                try
                {
                    WebManager.Post(
                        RedirectUri + "/com/oauth2/token",
                        new Dictionary<string, string>
                        {
                            { "code", s.Trim() },
                            { "client_id", "1unnzwkb8igx70k" },
                            { "client_secret", "3i5u3j3141php7u" },
                            { "grant_type", "authorization_code" }
                        },
                        new Dictionary<string, string>(),
                        new MemoryStream(),
                        loginProcessData.Progress,
                        delegate(byte[] result)
                        {
                            SettingsManager.CommunityAccessToken =
                                ((IDictionary<string, object>?)WebManager.JsonFromBytes(result))?["access_token"]
                                .ToString() ?? throw new InvalidOperationException("access_token is null");
                            loginProcessData.Succeed(this);
                        },
                        delegate(Exception error) { loginProcessData.Fail(this, error); });
                }
                catch (Exception err)
                {
                    loginProcessData.Fail(this, err);
                }
            });
        DialogsManager.ShowDialog(null, dialog);
    }

    public void HandleUri(Program.HandleUriItem uri)
    {
        _loginProcessData ??= new LoginProcessData
        {
            IsTokenFlow = true
        };
        var loginProcessData = _loginProcessData;
        _loginProcessData = null;
        if (!loginProcessData.IsTokenFlow || uri.Uri.Host != "login")
        {
            return;
        }

        try
        {
            if (uri == null || string.IsNullOrEmpty(uri.Uri.Fragment))
            {
                throw new Exception("不能接收来自SC中文社区的身份验证信息");
            }

            var dictionary = WebManager.UrlParametersFromString(uri.Uri.Fragment.TrimStart('#'));
            if (!dictionary.TryGetValue("access_token", out var accessToken))
            {
                if (dictionary.TryGetValue("error", out var ex))
                {
                    throw new Exception(ex);
                }

                throw new Exception("不能接收来自SC中文社区的身份验证信息");
            }

            SettingsManager.CommunityAccessToken = accessToken;
            loginProcessData.Succeed(this);
            uri.IsHandle = true;
        }
        catch (Exception error)
        {
            loginProcessData.Fail(this, error);
        }
    }

    public void VerifyLoggedIn()
    {
        if (!IsLoggedIn)
        {
            throw new InvalidOperationException("这个应用未登录到SC中文社区中国社区");
        }
    }

    internal static ExternalContentEntry JsonObjectToEntry(JsonObject? jsonObject)
    {
        var externalContentEntry = new ExternalContentEntry();
        if (jsonObject == null ||
            !jsonObject.ContainsKey("entries") ||
            jsonObject["entries"] is not JsonArray jsonArray)
        {
            return externalContentEntry;
        }

        foreach (var jsonNode in jsonArray)
        {
            if (jsonNode is not JsonObject item)
            {
                continue;
            }

            var externalContentEntry2 = new ExternalContentEntry
            {
                Path = item["path_display"]?.ToString() ?? string.Empty,
            };
            externalContentEntry2.Type = item[".tag"]?.ToString() == "folder"
                ? ExternalContentType.Directory
                : ExternalContentManager.ExtensionToType(Storage.GetExtension(externalContentEntry2.Path));
            if (externalContentEntry2.Type != ExternalContentType.Directory)
            {
                externalContentEntry2.Time = item.ContainsKey("server_modified")
                    ? DateTime.Parse(item["server_modified"]?.ToString() ?? string.Empty, CultureInfo.InvariantCulture)
                    : new DateTime(2000, 1, 1);
                externalContentEntry2.Size = item.ContainsKey("size") ? (long)(item["size"] ?? "0") : 0;
            }

            externalContentEntry.ChildEntries.Add(externalContentEntry2);
        }

        return externalContentEntry;
    }

    //获取分享连接
    internal static string JsonObjectToLinkAddress(JsonObject? jsonObject)
    {
        if (jsonObject == null || !jsonObject.TryGetPropertyValue("url", out var urlNode))
        {
            throw new InvalidOperationException("没有分享链接信息");
        }

        return urlNode?.ToString() ?? throw new InvalidOperationException("无效的分享链接");
    }

    public static string NormalizePath(string path)
    {
        if (path == "/")
        {
            return string.Empty;
        }

        if (path.Length > 0 && path[0] != '/')
        {
            return "/" + path;
        }

        return path;
    }

    public class LoginProcessData
    {
        public Action<Exception> Failure = delegate { };

        public bool IsTokenFlow;

        public CancellableProgress Progress = new();

        public Action Success = Actions.Empty;

        public void Succeed(SchubExternalContentProvider provider)
        {
            provider._loginProcessData = null;
            Success.Invoke();
        }

        public void Fail(SchubExternalContentProvider provider, Exception error)
        {
            provider._loginProcessData = null;
            Failure.Invoke(error);
        }
    }
}
