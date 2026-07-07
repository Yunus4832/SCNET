using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Game.ContentProviders;

public class DropboxExternalContentProvider : IExternalContentProvider
{
    private const string _appKey = "1unnzwkb8igx70k";

    private const string _appSecret = "3i5u3j3141php7u";

    private const string _redirectUri = PlatformManager.DropboxRedirectScheme + "://redirect";

    private LoginProcessData? _loginProcessData;

    public DropboxExternalContentProvider()
    {
        GameEntry.HandleUri += HandleUri;
        Window.Activated += WindowActivated;
    }

    public string DisplayName => "Dropbox";

    public string Description => !IsLoggedIn ? "Not logged in" : "Logged in";

    public bool SupportsListing => true;

    public bool SupportsLinks => true;

    public bool RequiresLogin => true;

    public bool IsLoggedIn => !string.IsNullOrEmpty(SettingsManager.Current.DropboxAccessToken);

    public void Dispose()
    {
        GameEntry.HandleUri -= HandleUri;
        Window.Activated -= WindowActivated;
    }

    public void Login(CancellableProgress progress, Action success, Action<Exception> failure)
    {
        try
        {
            if (_loginProcessData != null)
            {
                throw new InvalidOperationException("Login already in progress.");
            }

            if (!WebManager.IsInternetConnectionAvailable())
            {
                throw new InvalidOperationException("Internet connection is unavailable.");
            }

            Logout();
            progress.Cancelled += delegate
            {
                if (_loginProcessData == null)
                {
                    return;
                }

                var loginProcessData = _loginProcessData;
                _loginProcessData = null;
                loginProcessData.Fail(this, new OperationCanceledException());
            };
            _loginProcessData = new LoginProcessData
            {
                Progress = progress,
                Success = success,
                Failure = failure
            };
            LoginLaunchBrowser();
        }
        catch (Exception obj)
        {
            failure(obj);
        }
    }

    public void Logout()
    {
        SettingsManager.Current.DropboxAccessToken = string.Empty;
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
                { "Authorization", "Bearer " + SettingsManager.Current.DropboxAccessToken },
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
                "https://api.dropboxapi.com/2/files/list_folder",
                new Dictionary<string, string>(),
                dictionary,
                data,
                progress,
                delegate(byte[] result)
                {
                    try
                    {
                        var jsonObject2 = (JsonObject?)WebManager.JsonFromBytes(result);
                        success(JsonObjectToEntry(jsonObject2));
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
        Action<Exception> failure)
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
                { "Authorization", "Bearer " + SettingsManager.Current.DropboxAccessToken },
                { "Dropbox-API-Arg", jsonObject.ToString() }
            };
            WebManager.Get(
                "https://content.dropboxapi.com/2/files/download",
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

    public void Upload(string path, Stream stream, CancellableProgress progress, Action<string> success,
        Action<Exception> failure)
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
                { "Authorization", "Bearer " + SettingsManager.Current.DropboxAccessToken },
                { "Content-Type", "application/octet-stream" },
                { "Dropbox-API-Arg", jsonObject.ToString() }
            };
            WebManager.Post(
                "https://content.dropboxapi.com/2/files/upload",
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

    public void Link(string path, CancellableProgress progress, Action<string> success, Action<Exception> failure)
    {
        try
        {
            VerifyLoggedIn();
            var dictionary = new Dictionary<string, string>
            {
                { "Authorization", "Bearer " + SettingsManager.Current.DropboxAccessToken },
                { "Content-Type", "application/json" }
            };
            var jsonObject = new JsonObject
            {
                { "path", NormalizePath(path) },
                { "short_url", false }
            };
            var data = new MemoryStream(Encoding.UTF8.GetBytes(jsonObject.ToString()));
            WebManager.Post(
                "https://api.dropboxapi.com/2/sharing/create_shared_link",
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

    public void LoginLaunchBrowser()
    {
        try
        {
            _loginProcessData?.IsTokenFlow = true;
            var dictionary = new Dictionary<string, string>
            {
                { "response_type", "token" },
                { "client_id", "1unnzwkb8igx70k" },
                { "redirect_uri", _redirectUri }
            };
            WebBrowserManager.LaunchBrowser("https://www.dropbox.com/oauth2/authorize?" +
                                            WebManager.UrlParametersToString(dictionary));
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
            "Enter Dropbox authorization code",
            "",
            256,
            delegate(string s)
            {
                try
                {
                    WebManager.Post(
                        "https://api.dropboxapi.com/oauth2/token",
                        new Dictionary<string, string>
                        {
                            {
                                "code",
                                s.Trim()
                            },
                            {
                                "client_id",
                                "1unnzwkb8igx70k"
                            },
                            {
                                "client_secret",
                                "3i5u3j3141php7u"
                            },
                            {
                                "grant_type",
                                "authorization_code"
                            }
                        },
                        new Dictionary<string, string>(),
                        new MemoryStream(),
                        loginProcessData.Progress,
                        delegate(byte[] result)
                        {
                            var jsonObject = (JsonObject?)WebManager.JsonFromBytes(result);
                            SettingsManager.Current.DropboxAccessToken =
                                jsonObject?["access_token"]?.ToString()
                                ?? throw new InvalidOperationException("access_token is null");
                            loginProcessData.Succeed(this);
                        },
                        delegate(Exception error) { loginProcessData.Fail(this, error); }
                    );
                }
                catch (Exception error2)
                {
                    loginProcessData.Fail(this, error2);
                }
            });
        DialogsManager.ShowDialog(null, dialog);
    }

    public void HandleUri(GameEntry.HandleUriItem uri)
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

        Log.Information("[DROPBOX]URI::" + uri.Uri.Fragment);
        try
        {
            if (uri == null || string.IsNullOrEmpty(uri.Uri.Fragment))
            {
                throw new Exception("Could not retrieve Dropbox access token.");
            }

            var dictionary = WebManager.UrlParametersFromString(uri.Uri.Fragment.TrimStart('#'));
            if (!dictionary.TryGetValue("access_token", out var accessToken))
            {
                if (dictionary.TryGetValue("error", out var ex))
                {
                    throw new Exception(ex);
                }

                throw new Exception("Could not retrieve Dropbox access token.");
            }

            SettingsManager.Current.DropboxAccessToken = accessToken;
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
            throw new InvalidOperationException("Not logged in to Dropbox in this app.");
        }
    }

    public static ExternalContentEntry JsonObjectToEntry(JsonObject? jsonObject)
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

    public static string JsonObjectToLinkAddress(JsonObject? jsonObject)
    {
        if (jsonObject == null || !jsonObject.TryGetPropertyValue("url", out var urlNode))
        {
            throw new InvalidOperationException("Share information not found.");
        }

        var url = urlNode?.ToString() ?? throw new InvalidOperationException("Invalid shared link");
        return url.Replace("www.dropbox.", "dl.dropbox.").Replace("?dl=0", "") + "?dl=1";
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

        public void Succeed(DropboxExternalContentProvider provider)
        {
            provider._loginProcessData = null;
            Success.Invoke();
        }

        public void Fail(DropboxExternalContentProvider provider, Exception error)
        {
            provider._loginProcessData = null;
            Failure.Invoke(error);
        }
    }
}
