using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using EntitySystem.XmlUtilities;

using Game.ContentProviders;

namespace Game.Managers;

public static class CommunityContentManager
{
    private static readonly string _scResDirAddress = SchubExternalContentProvider.GetPath("/com/list");

    private static readonly Dictionary<string, string> _idToAddressMap = new();

    private static readonly Dictionary<string, bool> _feedbackCache = new();

    public static void Initialize()
    {
        Load();
        WorldsManager.WorldDeleted += delegate(string path)
        {
            _idToAddressMap.Remove(MakeContentIdString(ExternalContentType.World, path));
        };
        BlocksTexturesManager.BlocksTextureDeleted += delegate(string path)
        {
            _idToAddressMap.Remove(MakeContentIdString(ExternalContentType.BlocksTexture, path));
        };
        CharacterSkinsManager.CharacterSkinDeleted += delegate(string path)
        {
            _idToAddressMap.Remove(MakeContentIdString(ExternalContentType.CharacterSkin, path));
        };
        FurniturePacksManager.FurniturePackDeleted += delegate(string path)
        {
            _idToAddressMap.Remove(MakeContentIdString(ExternalContentType.FurniturePack, path));
        };
        Window.Deactivated += Save;
    }

    public static string GetDownloadedContentAddress(ExternalContentType type, string name)
    {
        _idToAddressMap.TryGetValue(MakeContentIdString(type, name), out var value);
        return value ?? string.Empty;
    }

    public static bool IsContentRated(string address, string userId)
    {
        var key = MakeFeedbackCacheKey(address, "Rating", userId);
        return _feedbackCache.ContainsKey(key);
    }

    public static void List(
        string cursor,
        string userFilter,
        string typeFilter,
        string moderationFilter,
        string sortOrder,
        string keySearch,
        CancellableProgress progress,
        Action<List<CommunityContentEntry>, string> success,
        Action<Exception> failure
    )
    {
        if (!WebManager.IsInternetConnectionAvailable())
        {
            failure(new InvalidOperationException("Internet connection is unavailable."));
            return;
        }

        var header = new Dictionary<string, string> { { "Content-Type", "application/x-www-form-urlencoded" } };
        var dictionary = new Dictionary<string, string>
        {
            { "Action", "list" },
            { "Cursor", cursor },
            { "UserId", userFilter },
            { "Type", typeFilter },
            { "Moderation", moderationFilter },
            { "SortOrder", sortOrder },
            { "Platform", VersionsManager.Platform.ToString() },
            { "Version", VersionsManager.Version },
            { "Apiv", ModsManager.ApiV.ToString() },
            { "key", keySearch }
        };
        WebManager.Post(
            _scResDirAddress,
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            delegate(byte[] result)
            {
                try
                {
                    var xElement = XmlUtils.LoadXmlFromString(Encoding.UTF8.GetString(result, 0, result.Length), true);
                    var attributeValue = XmlUtils.GetAttributeValue<string>(xElement, "NextCursor");
                    var list = new List<CommunityContentEntry>();
                    foreach (var item in xElement.Elements())
                    {
                        try
                        {
                            list.Add(new CommunityContentEntry
                            {
                                Type = XmlUtils.GetAttributeValue(item, "Type", ExternalContentType.Unknown),
                                Name = XmlUtils.GetAttributeValue<string>(item, "Name"),
                                Address = XmlUtils.GetAttributeValue<string>(item, "Url"),
                                UserId = XmlUtils.GetAttributeValue<string>(item, "UserId"),
                                Size = XmlUtils.GetAttributeValue<long>(item, "Size"),
                                ExtraText = XmlUtils.GetAttributeValue(item, "ExtraText", string.Empty),
                                RatingsAverage = XmlUtils.GetAttributeValue(item, "RatingsAverage", 0f),
                                IconSrc = XmlUtils.GetAttributeValue(item, "Icon", "")
                            });
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }

                    success(list, attributeValue);
                }
                catch (Exception obj)
                {
                    failure(obj);
                }
            },
            failure
        );
    }

    public static void Download(
        string address,
        string name,
        ExternalContentType type,
        string userId,
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        if (!WebManager.IsInternetConnectionAvailable())
        {
            failure(new InvalidOperationException("Internet connection is unavailable."));
        }
        else
        {
            WebManager.Get(
                address,
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                progress,
                delegate(byte[] data)
                {
                    var hash = CalculateContentHashString(data);
                    ExternalContentManager.ImportExternalContent(new MemoryStream(data), type, name,
                        delegate(string downloadedName)
                        {
                            _idToAddressMap[MakeContentIdString(type, downloadedName)] = address;
                            Feedback(
                                address,
                                "Success",
                                string.Empty,
                                hash,
                                data.Length,
                                userId,
                                progress,
                                Actions.Empty,
                                delegate { }
                            );
                            success();
                        },
                        delegate(Exception error)
                        {
                            Feedback(
                                address,
                                "ImportFailure",
                                string.Empty,
                                hash,
                                data.Length,
                                userId,
                                progress,
                                Actions.Empty,
                                delegate { }
                            );
                            failure(error);
                        });
                },
                delegate(Exception error)
                {
                    Feedback(
                        address,
                        "DownloadFailure",
                        string.Empty,
                        string.Empty,
                        0L,
                        userId,
                        progress,
                        Actions.Empty,
                        delegate { }
                    );
                    failure(error);
                }
            );
        }
    }

    public static void Publish(
        string address,
        string name,
        ExternalContentType type,
        string userId,
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        if (!WebManager.IsInternetConnectionAvailable())
        {
            failure(new InvalidOperationException("Internet connection is unavailable."));
        }
        else
        {
            VerifyLinkContent(
                address,
                name,
                type,
                progress,
                delegate(byte[] data)
                {
                    var value = CalculateContentHashString(data);
                    WebManager.Post(
                        _scResDirAddress,
                        new Dictionary<string, string>(),
                        new Dictionary<string, string>(),
                        WebManager.UrlParametersToStream(
                            new Dictionary<string, string>
                            {
                                {
                                    "Action",
                                    "publish"
                                },
                                {
                                    "UserId",
                                    userId
                                },
                                {
                                    "Name",
                                    name
                                },
                                {
                                    "Url",
                                    address
                                },
                                {
                                    "Type",
                                    type.ToString()
                                },
                                {
                                    "Hash",
                                    value
                                },
                                {
                                    "Size",
                                    data.Length.ToString(CultureInfo.InvariantCulture)
                                },
                                {
                                    "Platform",
                                    VersionsManager.Platform.ToString()
                                },
                                {
                                    "Version",
                                    VersionsManager.Version
                                }
                            }),
                        progress,
                        delegate { success(); },
                        failure
                    );
                },
                failure
            );
        }
    }

    public static void Delete(
        string address,
        string userId,
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        if (!WebManager.IsInternetConnectionAvailable())
        {
            failure(new InvalidOperationException("Internet connection is unavailable."));
            return;
        }

        var dictionary = new Dictionary<string, string>
        {
            { "Action", "delete" },
            { "UserId", userId },
            { "Url", address },
            { "Platform", VersionsManager.Platform.ToString() },
            { "Version", VersionsManager.Version }
        };
        WebManager.Post(
            _scResDirAddress,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            WebManager.UrlParametersToStream(dictionary),
            progress,
            delegate { success(); },
            failure
        );
    }

    public static void Rate(
        string address,
        string userId,
        int rating,
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        rating = MathUtils.Clamp(rating, 1, 5);
        Feedback(
            address,
            "Rating",
            rating.ToString(CultureInfo.InvariantCulture),
            string.Empty,
            0L,
            userId,
            progress,
            success,
            failure
        );
    }

    public static void Report(
        string address,
        string userId,
        string report,
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        Feedback(
            address,
            "Report",
            report,
            string.Empty,
            0L,
            userId,
            progress,
            success,
            failure
        );
    }

    public static void SendPlayTime(
        string address,
        string userId,
        double time,
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        Feedback(
            address,
            "PlayTime",
            MathUtils.Round(time).ToString(CultureInfo.InvariantCulture),
            string.Empty,
            0L,
            userId,
            progress,
            success,
            failure
        );
    }

    public static void VerifyLinkContent(
        string address,
        string name,
        ExternalContentType type,
        CancellableProgress progress,
        Action<byte[]> success,
        Action<Exception> failure
    )
    {
        WebManager.Get(
            address,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            progress,
            delegate(byte[] data)
            {
                ExternalContentManager.ImportExternalContent(
                    new MemoryStream(data),
                    type,
                    "__Temp",
                    delegate(string downloadedName)
                    {
                        ExternalContentManager.DeleteExternalContent(type, downloadedName);
                        success(data);
                    },
                    failure
                );
            },
            failure
        );
    }

    private static void Feedback(
        string address,
        string feedback,
        string feedbackParameter,
        string hash,
        long size,
        string userId,
        CancellableProgress progress,
        Action success,
        Action<Exception> failure
    )
    {
        if (!WebManager.IsInternetConnectionAvailable())
        {
            failure(new InvalidOperationException("Internet connection is unavailable."));
            return;
        }

        var dictionary = new Dictionary<string, string>
        {
            { "Action", "feedback" },
            { "Feedback", feedback },
            { "UserId", userId }
        };

        if (!string.IsNullOrEmpty(feedbackParameter))
        {
            dictionary.Add("FeedbackParameter", feedbackParameter);
        }

        if (!string.IsNullOrEmpty(address))
        {
            dictionary.Add("Url", address);
        }

        if (!string.IsNullOrEmpty(hash))
        {
            dictionary.Add("Hash", hash);
        }

        if (size > 0)
        {
            dictionary.Add("Size", size.ToString(CultureInfo.InvariantCulture));
        }

        dictionary.Add("Platform", VersionsManager.Platform.ToString());
        dictionary.Add("Version", VersionsManager.Version);
        WebManager.Post(
            _scResDirAddress,
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            WebManager.UrlParametersToStream(dictionary),
            progress,
            delegate
            {
                var key = MakeFeedbackCacheKey(address, feedback, userId);
                if (!_feedbackCache.TryAdd(key, true))
                {
                    Task.Run(delegate
                    {
                        Task.Delay(1500).Wait();
                        failure(new InvalidOperationException("Duplicate feedback."));
                    });
                    return;
                }

                success();
            },
            failure
        );
    }

    public static string CalculateContentHashString(byte[] data)
    {
        return Convert.ToBase64String(SHA1.HashData(data));
    }

    public static string MakeFeedbackCacheKey(string address, string feedback, string userId)
    {
        return address + "\n" + feedback + "\n" + userId;
    }

    public static string MakeContentIdString(ExternalContentType type, string name)
    {
        return type + ":" + name;
    }

    public static void Load()
    {
        try
        {
            if (!Storage.FileExists(ModsManager.CommunityContentCachePath))
            {
                return;
            }

            using var stream = Storage.OpenFile(ModsManager.CommunityContentCachePath, OpenFileMode.Read);
            var xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
            foreach (var item in xElement.Element("Feedback")?.Elements() ?? [])
            {
                var attributeValue = XmlUtils.GetAttributeValue<string>(item, "Key");
                _feedbackCache[attributeValue] = true;
            }

            foreach (var item2 in xElement.Element("Content")?.Elements() ?? [])
            {
                var attributeValue2 = XmlUtils.GetAttributeValue<string>(item2, "Path");
                var attributeValue3 = XmlUtils.GetAttributeValue<string>(item2, "Address");
                _idToAddressMap[attributeValue2] = attributeValue3;
            }
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser("Loading Community Content cache failed.", e);
        }
    }

    public static void Save()
    {
        try
        {
            var xElement = new XElement("Cache");
            var xElement2 = new XElement("Feedback");
            xElement.Add(xElement2);
            foreach (var key in _feedbackCache.Keys)
            {
                var xElement3 = new XElement("Item");
                XmlUtils.SetAttributeValue(xElement3, "Key", key);
                xElement2.Add(xElement3);
            }

            var xElement4 = new XElement("Content");
            xElement.Add(xElement4);
            foreach (var item in _idToAddressMap)
            {
                var xElement5 = new XElement("Item");
                XmlUtils.SetAttributeValue(xElement5, "Path", item.Key);
                XmlUtils.SetAttributeValue(xElement5, "Address", item.Value);
                xElement4.Add(xElement5);
            }

            using var stream = Storage.OpenFile(ModsManager.CommunityContentCachePath, OpenFileMode.Create);
            XmlUtils.SaveXmlToStream(xElement, stream, null, true);
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser("Saving Community Content cache failed.", e);
        }
    }

    public static void List(
        string cursor,
        string userFilter,
        string typeFilter,
        string moderationFilter,
        string sortOrder,
        string keySearch,
        string searchType,
        CancellableProgress progress,
        Action<List<CommunityContentEntry>, string> success,
        Action<Exception> failure
    )
    {
        if (!WebManager.IsInternetConnectionAvailable())
        {
            failure(new InvalidOperationException("Internet connection is unavailable."));
            return;
        }

        var header = new Dictionary<string, string> { { "Content-Type", "application/x-www-form-urlencoded" } };
        var dictionary = new Dictionary<string, string>
        {
            { "Action", "list" },
            { "Cursor", cursor },
            { "UserId", userFilter },
            { "Type", typeFilter },
            { "Moderation", moderationFilter },
            { "SortOrder", sortOrder },
            { "Platform", VersionsManager.Platform.ToString() },
            { "Version", VersionsManager.Version },
            { "Apiv", ModsManager.ApiV.ToString() },
            { "key", keySearch },
            { "SearchType", searchType }
        };
        WebManager.Post(
            _scResDirAddress,
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            delegate(byte[] result)
            {
                try
                {
                    var xElement = XmlUtils.LoadXmlFromString(Encoding.UTF8.GetString(result, 0, result.Length), true);
                    var attributeValue = XmlUtils.GetAttributeValue<string>(xElement, "NextCursor");
                    var list = new List<CommunityContentEntry>();
                    foreach (var item in xElement.Elements())
                    {
                        try
                        {
                            list.Add(new CommunityContentEntry
                            {
                                Type = XmlUtils.GetAttributeValue(item, "Type", ExternalContentType.Unknown),
                                Name = XmlUtils.GetAttributeValue(item, "Name", string.Empty),
                                Address = XmlUtils.GetAttributeValue(item, "Url", string.Empty),
                                UserId = XmlUtils.GetAttributeValue(item, "UserId", string.Empty),
                                UserName = XmlUtils.GetAttributeValue(item, "UName", string.Empty),
                                Boutique = XmlUtils.GetAttributeValue(item, "Boutique", 0),
                                IsShow = XmlUtils.GetAttributeValue(item, "IsShow", 0),
                                Size = XmlUtils.GetAttributeValue<long>(item, "Size", 0),
                                ExtraText = XmlUtils.GetAttributeValue(item, "ExtraText", string.Empty),
                                RatingsAverage = XmlUtils.GetAttributeValue(item, "RatingsAverage", 0f),
                                IconSrc = XmlUtils.GetAttributeValue(item, "Icon", string.Empty),
                                Index = XmlUtils.GetAttributeValue(item, "Id", 0)
                            });
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }

                    success(list, attributeValue);
                }
                catch (Exception obj)
                {
                    failure(obj);
                }
            },
            failure
        );
    }


    public static void UserList(
        string cursor,
        string searchKey,
        string searchType,
        string filter,
        int order,
        CancellableProgress progress,
        Action<List<ManageUserScreen.ComUserInfo>, string> success,
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
            { "Cursor", cursor },
            { "Action", "GetUserList" },
            { "Operater", SettingsManager.CommunityAccessToken },
            { "SearchKey", searchKey },
            { "SearchType", searchType },
            { "Filter", filter },
            { "Order", order.ToString() }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/userList"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            delegate(byte[] result)
            {
                try
                {
                    var xElement = XmlUtils.LoadXmlFromString(Encoding.UTF8.GetString(result, 0, result.Length), true);
                    var attributeValue = XmlUtils.GetAttributeValue<string>(xElement, "NextCursor");
                    var list = new List<ManageUserScreen.ComUserInfo>();
                    foreach (var item in xElement.Elements())
                    {
                        try
                        {
                            list.Add(new ManageUserScreen.ComUserInfo
                            {
                                Id = XmlUtils.GetAttributeValue<int>(item, "Id"),
                                UserNo = XmlUtils.GetAttributeValue<string>(item, "User"),
                                Name = XmlUtils.GetAttributeValue<string>(item, "Nickname"),
                                Token = XmlUtils.GetAttributeValue<string>(item, "Token"),
                                LastLoginTime = XmlUtils.GetAttributeValue<string>(item, "LastLoginTime"),
                                ErrCount = XmlUtils.GetAttributeValue(item, "ErrorTimes", 0),
                                IsLock = XmlUtils.GetAttributeValue(item, "IsLock", 0),
                                LockTime = XmlUtils.GetAttributeValue<string>(item, "LockTime"),
                                UnlockTime = XmlUtils.GetAttributeValue<string>(item, "UnlockTime"),
                                LockDuration = XmlUtils.GetAttributeValue(item, "LockDuration", 0),
                                Money = XmlUtils.GetAttributeValue(item, "Money", 0),
                                Authority = XmlUtils.GetAttributeValue<string>(item, "Authority"),
                                HeadImg = XmlUtils.GetAttributeValue<string>(item, "HeadImg"),
                                IsAdmin = XmlUtils.GetAttributeValue(item, "IsAdmin", 0),
                                RegTime = XmlUtils.GetAttributeValue<string>(item, "RegTime"),
                                LoginIP = XmlUtils.GetAttributeValue<string>(item, "LoginIP"),
                                MGroup = XmlUtils.GetAttributeValue<string>(item, "MGroup"),
                                PawToken = XmlUtils.GetAttributeValue<string>(item, "PassToken"),
                                Email = XmlUtils.GetAttributeValue<string>(item, "Email"),
                                Status = XmlUtils.GetAttributeValue(item, "Status", 1),
                                LockReason = XmlUtils.GetAttributeValue<string>(item, "LockReason"),
                                EmailCount = XmlUtils.GetAttributeValue(item, "EmailCount", 0),
                                EmailTime = XmlUtils.GetAttributeValue<string>(item, "EmailTime"),
                                Die = XmlUtils.GetAttributeValue(item, "Die", 0),
                                Moblie = XmlUtils.GetAttributeValue<string>(item, "Moblie"),
                                AreaCode = XmlUtils.GetAttributeValue<string>(item, "AreaCode")
                            });
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }

                    success(list, attributeValue);
                }
                catch (Exception obj)
                {
                    failure(obj);
                }
            },
            failure
        );
    }

    public static void UpdateLockState(
        int id,
        int lockState,
        string reason,
        int duration,
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
            { "Action", "UpdateLockState" },
            { "Id", id.ToString() },
            { "Operater", SettingsManager.CommunityAccessToken },
            { "LockState", lockState.ToString() },
            { "Duration", duration.ToString() },
            { "Reason", reason }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/userList"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            success,
            failure
        );
    }

    public static void ResetPassword(
        int id,
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
            { "Action", "ResetPassword" },
            { "Id", id.ToString() },
            { "Operater", SettingsManager.CommunityAccessToken }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/userList"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            success,
            failure
        );
    }

    public static void UpdateBoutique(
        string type,
        int id,
        int boutique,
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
            { "Type", type },
            { "Id", id.ToString() },
            { "Operater", SettingsManager.CommunityAccessToken },
            { "Boutique", boutique.ToString() }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/boutique"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            success,
            failure
        );
    }

    public static void UpdateHidePara(
        int id,
        int isShow,
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
            { "Id", id.ToString() },
            { "Operater", SettingsManager.CommunityAccessToken },
            { "IsShow", isShow.ToString() }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/hide"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            success,
            failure
        );
    }

    public static void DeleteFile(
        int id,
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
            { "Id", id.ToString() },
            { "Operater", SettingsManager.CommunityAccessToken }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/deleteFile"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            success,
            failure
        );
    }


    public static void IsAdmin(
        CancellableProgress progress,
        Action<bool> success,
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
            { "Operater", SettingsManager.CommunityAccessToken }
        };
        WebManager.Post(
            SchubExternalContentProvider.GetPath("/com/api/zh/userList"),
            new Dictionary<string, string>(),
            header,
            WebManager.UrlParametersToStream(dictionary),
            progress,
            delegate(byte[] data)
            {
                if (WebManager.JsonFromBytes(data) is not JsonObject jsonObject)
                {
                    return;
                }

                if (!jsonObject.TryGetPropertyValue("code", out var codeNode) || codeNode is null)
                {
                    return;
                }

                success(codeNode.ToString() == "200");
            },
            failure
        );
    }
}
