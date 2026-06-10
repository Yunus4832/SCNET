using System.Text.Json.Nodes;
using System.Xml.Linq;

using Engine.Graphics;
using Engine.Media;

using Game.ContentProviders;

namespace Game.Screens;

public class CommunityContentScreen : Screen
{
    public enum Order
    {
        ByRank,
        ByTime,
        ByBoutique,
        ByHide
    }

    public enum SearchType
    {
        ByName,
        ByAuthor,
        ByUserId
    }

    private readonly ButtonWidget _action2Button;

    private readonly ButtonWidget _action3Button;

    private readonly ButtonWidget _actionButton;

    private readonly ButtonWidget _changeFilterButton;

    private readonly ButtonWidget _changeOrderButton;

    private double _contentExpiryTime;

    private readonly ButtonWidget _downloadButton;

    private object? _filter;

    private readonly LabelWidget _filterLabel;

    private readonly TextBoxWidget _inputKey;

    private bool _isAdmin;

    private bool _isCnLanguageType;

    private bool _isOwn;

    public Dictionary<string, IEnumerable<object>> ItemsCache = new();

    private readonly ListPanelWidget _listPanel;

    private LinkWidget? _moreLink;

    private readonly ButtonWidget _moreOptionsButton;

    private Order _order;

    private readonly LabelWidget _orderLabel;

    private readonly LabelWidget _placeHolder;

    private SchubExternalContentProvider? _provider;

    private readonly ButtonWidget _searchKey;

    private SearchType _searchType;

    private readonly ButtonWidget _searchTypeButton;

    public CommunityContentScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/CommunityContentScreen");
        LoadContents(this, node);
        _listPanel = Children.Find<ListPanelWidget>("List")!;
        _orderLabel = Children.Find<LabelWidget>("Order")!;
        _changeOrderButton = Children.Find<ButtonWidget>("ChangeOrder")!;
        _filterLabel = Children.Find<LabelWidget>("Filter")!;
        _changeFilterButton = Children.Find<ButtonWidget>("ChangeFilter")!;
        _downloadButton = Children.Find<ButtonWidget>("Download")!;
        _actionButton = Children.Find<ButtonWidget>("Action")!;
        _action2Button = Children.Find<ButtonWidget>("Action2")!;
        _action3Button = Children.Find<ButtonWidget>("Action3")!;
        _moreOptionsButton = Children.Find<ButtonWidget>("MoreOptions")!;
        _inputKey = Children.Find<TextBoxWidget>("key")!;
        _placeHolder = Children.Find<LabelWidget>("placeholder")!;
        _searchKey = Children.Find<ButtonWidget>("Search")!;
        _searchTypeButton = Children.Find<ButtonWidget>("SearchType")!;
        _searchType = SearchType.ByName;
        _listPanel.ItemWidgetFactory = delegate(object item)
        {
            if (item is CommunityContentEntry communityContentEntry)
            {
                var node2 = ContentManager.Get<XElement>("Widgets/CommunityContentItem");
                var obj = (ContainerWidget)LoadWidget(this, node2, null);
                communityContentEntry.IconInstance = obj.Children.Find<RectangleWidget>("CommunityContentItem.Icon")!;
                communityContentEntry.IconInstance.Subtexture = communityContentEntry.Icon == null
                    ? ExternalContentManager.GetEntryTypeIcon(communityContentEntry.Type)
                    : new Subtexture(communityContentEntry.Icon, Vector2.Zero, Vector2.One);
                obj.Children.Find<LabelWidget>("CommunityContentItem.Text")!.Text = communityContentEntry.Name;
                var txtColor = Color.White;
                if (communityContentEntry.Boutique > 0)
                {
                    txtColor = new Color(255, 215, 0);
                }

                if (_isOwn && communityContentEntry.IsShow == 0)
                {
                    txtColor = Color.Gray;
                }

                obj.Children.Find<LabelWidget>("CommunityContentItem.Text")!.Color = txtColor;
                obj.Children.Find<LabelWidget>("CommunityContentItem.Details")!.Text =
                    $"{ExternalContentManager.GetEntryTypeDescription(communityContentEntry.Type)} {DataSizeFormatter.Format(communityContentEntry.Size)}";
                obj.Children.Find<StarRatingWidget>("CommunityContentItem.Rating")!.Rating =
                    communityContentEntry.RatingsAverage;
                obj.Children.Find<StarRatingWidget>("CommunityContentItem.Rating")!.IsVisible =
                    communityContentEntry.RatingsAverage > 0f;
                obj.Children.Find<LabelWidget>("CommunityContentItem.ExtraText")!.Text =
                    communityContentEntry.ExtraText;
                return obj;
            }

            var node3 = ContentManager.Get<XElement>("Widgets/CommunityContentItemMore");
            var containerWidget = (ContainerWidget)LoadWidget(this, node3, null);
            _moreLink = containerWidget.Children.Find<LinkWidget>("CommunityContentItemMore.Link")!;
            _moreLink.Tag = item as string ?? string.Empty;
            return containerWidget;
        };
        _listPanel.SelectionChanged += delegate
        {
            if (_listPanel.SelectedItem != null && !(_listPanel.SelectedItem is CommunityContentEntry))
            {
                _listPanel.SelectedItem = null;
            }
        };
    }

    public override void Enter(object[] parameters)
    {
        foreach (var provider in ExternalContentManager.Providers)
        {
            if (provider is not SchubExternalContentProvider contentProvider)
            {
                continue;
            }

            _provider = contentProvider;
            break;
        }

        _filter = string.Empty;

        _order = Order.ByRank;
        _inputKey.Text = string.Empty;
        _isOwn = false;
        var languageType = !AppConfigStore.Values.TryGetValue("Language", out var config) ? "zh-CN" : config;
        _isCnLanguageType = languageType == "zh-CN";
        CommunityContentManager.IsAdmin(new CancellableProgress(), delegate(bool isAdmin) { _isAdmin = isAdmin; },
            delegate(Exception e)
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        LanguageManager.Error,
                        e.Message,
                        LanguageManager.Ok)
                );
            });
        PopulateList(string.Empty);
    }

    public override void Update()
    {
        _placeHolder.IsVisible = string.IsNullOrEmpty(_inputKey.Text);
        _actionButton.IsVisible = _isAdmin || _isOwn;
        _action2Button.IsVisible = _isAdmin || _isOwn;
        if (!_isCnLanguageType)
        {
            _actionButton.IsVisible = false;
            _action2Button.IsVisible = false;
            _action3Button.IsVisible = false;
        }

        var communityContentEntry = _listPanel.SelectedItem as CommunityContentEntry;
        _downloadButton.IsEnabled = communityContentEntry != null;
        if (communityContentEntry != null)
        {
            _actionButton.IsEnabled = _isAdmin || _isOwn;
            if (_order == Order.ByHide || _isOwn)
            {
                _actionButton.Text = LanguageManager.Get(GetType().Name, 23);
            }
            else
            {
                _actionButton.Text = communityContentEntry.Boutique == 0
                    ? LanguageManager.Get(GetType().Name, 15)
                    : LanguageManager.Get(GetType().Name, 16);
            }

            _action2Button.IsEnabled = _isAdmin || _isOwn;
        }
        else
        {
            _actionButton.IsEnabled = false;
            _action2Button.IsEnabled = false;
            _actionButton.Text = LanguageManager.Get(GetType().Name, 17);
        }

        if (_isOwn)
        {
            _searchType = SearchType.ByName;
            _searchTypeButton.IsEnabled = false;
        }
        else
        {
            _searchTypeButton.IsEnabled = true;
        }

        _action2Button.Text = communityContentEntry is { IsShow: 0 }
            ? LanguageManager.Get(GetType().Name, 24)
            : LanguageManager.Get(GetType().Name, 25);
        _orderLabel.Text = GetOrderDisplayName(_order);
        _filterLabel.Text = GetFilterDisplayName(_filter);
        _searchTypeButton.Text = GetSearchTypeDisplayName(_searchType);
        if (_changeOrderButton.IsClicked)
        {
            var items = EnumUtils.GetEnumValues(typeof(Order)).Cast<Order>().ToList();
            if (!_isAdmin)
            {
                items.Remove(Order.ByHide);
            }

            DialogsManager.ShowDialog(null, new ListSelectionDialog(LanguageManager.Get(GetType().Name, "Order Type"),
                items, 60f, item => GetOrderDisplayName((Order)item), delegate(object item)
                {
                    _order = (Order)item;
                    PopulateList(string.Empty, true);
                }));
        }

        if (_searchKey.IsClicked)
        {
            PopulateList(string.Empty);
        }

        if (_changeFilterButton.IsClicked)
        {
            var list = new List<object>();
            list.Add(string.Empty);
            foreach (var item in from ExternalContentType t in
                         EnumUtils.GetEnumValues(typeof(ExternalContentType))
                     where ExternalContentManager.IsEntryTypeDownloadSupported(t)
                     select t)
            {
                list.Add(item);
            }

            if (!string.IsNullOrEmpty(SettingsManager.CommunityAccessToken))
            {
                list.Add(SettingsManager.CommunityAccessToken);
            }

            DialogsManager.ShowDialog(null, new ListSelectionDialog(LanguageManager.Get(GetType().Name, "Filter"), list,
                60f, item => GetFilterDisplayName(item), delegate(object item)
                {
                    _filter = item;
                    _isOwn = GetFilterDisplayName(item) == "只看自己";
                    PopulateList(string.Empty, true);
                }));
        }

        if (_downloadButton.IsClicked && communityContentEntry != null)
        {
            DownloadEntry(communityContentEntry);
        }

        if (_actionButton.IsClicked && communityContentEntry != null)
        {
            if (_order == Order.ByHide || _isOwn)
            {
                DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Get(GetType().Name, 26),
                    communityContentEntry.Name, LanguageManager.Ok, LanguageManager.Cancel,
                    delegate(MessageDialogButton button)
                    {
                        if (button != MessageDialogButton.Button1)
                        {
                            return;
                        }

                        var busyDialog = new CancellableBusyDialog(LanguageManager.Get(GetType().Name, 2), false);
                        DialogsManager.ShowDialog(null, busyDialog);
                        CommunityContentManager.DeleteFile(communityContentEntry.Index, busyDialog.Progress,
                            delegate(byte[] data)
                            {
                                DialogsManager.HideDialog(busyDialog);
                                _listPanel.RemoveItem(communityContentEntry);
                                if (WebManager.JsonFromBytes(data) is not JsonObject result)
                                {
                                    return;
                                }

                                var msg = result[0]?.ToString() == "200"
                                    ? LanguageManager.Get(GetType().Name, 27) + communityContentEntry.Name
                                    : result[1]?.ToString() ?? string.Empty;
                                DialogsManager.ShowDialog(
                                    null,
                                    new MessageDialog(
                                        LanguageManager.Get(GetType().Name, 20),
                                        msg,
                                        LanguageManager.Ok
                                    )
                                );
                            },
                            delegate(Exception e)
                            {
                                DialogsManager.HideDialog(busyDialog);
                                DialogsManager.ShowDialog(null,
                                    new MessageDialog(LanguageManager.Error, e.Message, LanguageManager.Ok));
                            });
                    }));
            }
            else
            {
                if (communityContentEntry.Boutique == 0)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new TextBoxDialog(
                            LanguageManager.Get(GetType().Name, 18),
                            "5",
                            4,
                            delegate(string s)
                            {
                                if (string.IsNullOrEmpty(s))
                                {
                                    return;
                                }

                                var boutique = 5;
                                try
                                {
                                    boutique = int.Parse(s);
                                }
                                catch
                                {
                                    // ignored
                                }

                                var busyDialog =
                                    new CancellableBusyDialog(LanguageManager.Get(GetType().Name, 2), false);
                                DialogsManager.ShowDialog(null, busyDialog);
                                CommunityContentManager.UpdateBoutique(communityContentEntry.Type.ToString(),
                                    communityContentEntry.Index, boutique, busyDialog.Progress, delegate(byte[] data)
                                    {
                                        DialogsManager.HideDialog(busyDialog);
                                        _order = Order.ByBoutique;
                                        PopulateList(string.Empty, true);
                                        if (WebManager.JsonFromBytes(data) is not JsonObject result)
                                        {
                                            return;
                                        }

                                        var msg = result[0]?.ToString() == "200"
                                            ? LanguageManager.Get(GetType().Name, 19) + communityContentEntry.Name
                                            : result[1]?.ToString() ?? string.Empty;
                                        DialogsManager.ShowDialog(
                                            null,
                                            new MessageDialog(
                                                LanguageManager.Get(GetType().Name, 20),
                                                msg,
                                                LanguageManager.Ok
                                            )
                                        );
                                    },
                                    delegate(Exception e)
                                    {
                                        DialogsManager.HideDialog(busyDialog);
                                        DialogsManager.ShowDialog(null,
                                            new MessageDialog(LanguageManager.Error, e.Message, LanguageManager.Ok));
                                    }
                                );
                            }
                        )
                    );
                }
                else
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(LanguageManager.Get(GetType().Name, 21),
                            communityContentEntry.Name,
                            LanguageManager.Ok,
                            LanguageManager.Cancel,
                            delegate(MessageDialogButton button)
                            {
                                if (button != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                var busyDialog =
                                    new CancellableBusyDialog(LanguageManager.Get(GetType().Name, 2), false);
                                DialogsManager.ShowDialog(null, busyDialog);
                                CommunityContentManager.UpdateBoutique(
                                    communityContentEntry.Type.ToString(),
                                    communityContentEntry.Index,
                                    0,
                                    busyDialog.Progress,
                                    delegate(byte[] data)
                                    {
                                        DialogsManager.HideDialog(busyDialog);
                                        PopulateList(string.Empty, true);
                                        if (WebManager.JsonFromBytes(data) is not JsonObject result)
                                        {
                                            return;
                                        }

                                        var msg = result[0]?.ToString() == "200"
                                            ? LanguageManager.Get(GetType().Name, 22) + communityContentEntry.Name
                                            : result[1]?.ToString() ?? string.Empty;
                                        DialogsManager.ShowDialog(null,
                                            new MessageDialog(
                                                LanguageManager.Get(GetType().Name, 20),
                                                msg,
                                                LanguageManager.Ok)
                                        );
                                    },
                                    delegate(Exception e)
                                    {
                                        DialogsManager.HideDialog(busyDialog);
                                        DialogsManager.ShowDialog(null,
                                            new MessageDialog(LanguageManager.Error, e.Message, LanguageManager.Ok));
                                    });
                            }));
                }
            }
        }

        if (_action2Button.IsClicked && communityContentEntry != null)
        {
            var busyDialog = new CancellableBusyDialog(LanguageManager.Get(GetType().Name, 2), false);
            DialogsManager.ShowDialog(null, busyDialog);
            var isShow = (communityContentEntry.IsShow + 1) % 2;
            var sucessMsg = isShow == 1
                ? LanguageManager.Get(GetType().Name, 28)
                : LanguageManager.Get(GetType().Name, 29);
            CommunityContentManager.UpdateHidePara(
                communityContentEntry.Index,
                isShow,
                busyDialog.Progress,
                delegate(byte[] data)
                {
                    DialogsManager.HideDialog(busyDialog);
                    if (!_isOwn)
                    {
                        _listPanel.RemoveItem(communityContentEntry);
                    }
                    else
                    {
                        PopulateList(string.Empty, true);
                    }

                    if (WebManager.JsonFromBytes(data) is not JsonObject result)
                    {
                        return;
                    }

                    var msg = result[0]?.ToString() == "200"
                        ? sucessMsg + communityContentEntry.Name
                        : result[1]?.ToString() ?? string.Empty;
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(LanguageManager.Get(GetType().Name, 20),
                            msg,
                            LanguageManager.Ok
                        )
                    );
                }, delegate(Exception e)
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.ShowDialog(null,
                        new MessageDialog(LanguageManager.Error, e.Message, LanguageManager.Ok));
                });
        }

        _action3Button.Text = "申精";
        if (_action3Button.IsClicked)
        {
            const string msg =
                "如果你觉得你的作品足够优秀，\n可以申请加入精品区，让更多人看到。\n加精作品将会是社区认证的作品，是有机会上游戏公告推广的。\n\n具体申精方式\n请加[SC中文社区存档交流群(745540296)]了解。\n同时，如果你对某个作品有异议，\n也可加群举报，本群会受理作品归属问题，守护玩家的劳动成果！\n";
            DialogsManager.ShowDialog(null, new MessageDialog("作品如何申精？", msg, LanguageManager.Ok));
        }

        if (_searchTypeButton.IsClicked)
        {
            if (_isAdmin)
            {
                _searchType = _searchType switch
                {
                    SearchType.ByName => SearchType.ByAuthor,
                    SearchType.ByAuthor => SearchType.ByUserId,
                    SearchType.ByUserId => SearchType.ByName,
                    _ => _searchType
                };
            }
            else
            {
                _searchType = _searchType switch
                {
                    SearchType.ByName => SearchType.ByAuthor,
                    SearchType.ByAuthor or SearchType.ByUserId => SearchType.ByName,
                    _ => _searchType
                };
            }
        }

        if (_moreOptionsButton.IsClicked)
        {
            if (_provider is { IsLoggedIn: true })
            {
                var info = string.IsNullOrEmpty(SettingsManager.ScpboxUserInfo)
                    ? "暂无用户信息"
                    : SettingsManager.ScpboxUserInfo;
                DialogsManager.ShowDialog(null, new MessageDialog("账号已登录,是否登出?", info, LanguageManager.Yes,
                    LanguageManager.No, delegate(MessageDialogButton button)
                    {
                        if (button == MessageDialogButton.Button1)
                        {
                            _provider.Logout();
                        }
                    }));
            }
            else
            {
                if (_provider != null)
                {
                    ExternalContentManager.ShowLoginUiIfNeeded(_provider, false, Actions.Empty);
                }
            }
        }

        if (_moreLink is { IsClicked: true })
        {
            PopulateList((string)_moreLink.Tag);
        }

        if (Input.Back || Children.Find<BevelledButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }

        if (Input is not { Hold.Y: < 20f, HoldTime: > 2f })
        {
            return;
        }

        _contentExpiryTime = 0.0;
        Task.Delay(250).Wait();
    }

    public void PopulateList(string cursor, bool force = false)
    {
        var text = SettingsManager.CommunityContentMode switch
        {
            CommunityContentMode.Strict => "1",
            CommunityContentMode.Normal => "0",
            _ => string.Empty
        };
        var text2 = _filter as string ?? string.Empty;
        var text3 = _filter is ExternalContentType
            ? LanguageManager.Get(GetType().Name, _filter.ToString() ?? string.Empty)
            : string.Empty;
        var text4 = _order.ToString();
        var cacheKey = text2 + "\n" + text3 + "\n" + text4 + "\n" + text + "\n" + _inputKey.Text;
        _moreLink = null;
        if (string.IsNullOrEmpty(cursor) && !force)
        {
            _listPanel.ClearItems();
            _listPanel.ScrollPosition = 0f;
            if (_contentExpiryTime != 0.0 && Time.RealTime < _contentExpiryTime &&
                ItemsCache.TryGetValue(cacheKey, out var value))
            {
                foreach (var item in value)
                {
                    _listPanel.AddItem(item);
                }

                return;
            }
        }

        if (force)
        {
            _listPanel.ClearItems();
        }

        var busyDialog = new CancellableBusyDialog(LanguageManager.Get(GetType().Name, 2), false);
        DialogsManager.ShowDialog(null, busyDialog);
        CommunityContentManager.List(cursor, text2, text3, text, text4, _inputKey.Text, _searchType.ToString(),
            busyDialog.Progress, delegate(List<CommunityContentEntry> list, string nextCursor)
            {
                DialogsManager.HideDialog(busyDialog);
                _contentExpiryTime = Time.RealTime + 300.0;
                while (_listPanel.Items.Count > 0 &&
                       !(_listPanel.Items[_listPanel.Items.Count - 1] is CommunityContentEntry))
                {
                    _listPanel.RemoveItemAt(_listPanel.Items.Count - 1);
                }

                foreach (var item2 in list)
                {
                    _listPanel.AddItem(item2);
                    if (item2.Icon == null && !string.IsNullOrEmpty(item2.IconSrc))
                    {
                        WebManager.Get(
                            item2.IconSrc,
                            new Dictionary<string, string>(),
                            new Dictionary<string, string>(),
                            new CancellableProgress(),
                            delegate(byte[] data)
                            {
                                Dispatcher.Dispatch(delegate
                                {
                                    if (data.Length <= 0)
                                    {
                                        return;
                                    }

                                    try
                                    {
                                        var texture = Texture2D.Load(Image.Load(new MemoryStream(data),
                                            ImageFileFormat.Png));
                                        item2.Icon = texture;
                                        item2.IconInstance?.Subtexture =
                                            new Subtexture(texture, Vector2.Zero, Vector2.One);
                                    }
                                    catch (Exception)
                                    {
                                        // ignored
                                    }
                                });
                            },
                            delegate { }
                        );
                    }
                    else
                    {
                        if (item2.Icon != null)
                        {
                            item2.IconInstance?.Subtexture = new Subtexture(item2.Icon, Vector2.Zero, Vector2.One);
                        }
                    }
                }

                if (list.Count > 0 && !string.IsNullOrEmpty(nextCursor))
                {
                    _listPanel.AddItem(nextCursor);
                }

                ItemsCache[cacheKey] = new List<object>(_listPanel.Items);
            },
            delegate(Exception error)
            {
                DialogsManager.HideDialog(busyDialog);
                DialogsManager.ShowDialog(null,
                    new MessageDialog(LanguageManager.Error, error.Message, LanguageManager.Ok));
            });
    }

    public void DownloadEntry(CommunityContentEntry entry)
    {
        var userId = UserManager.ActiveUser != null ? UserManager.ActiveUser.UniqueId : string.Empty;
        var busyDialog =
            new CancellableBusyDialog(string.Format(LanguageManager.Get(GetType().Name, 1), entry.Name), false);
        DialogsManager.ShowDialog(null, busyDialog);
        CommunityContentManager.Download(entry.Address, entry.Name, entry.Type, userId, busyDialog.Progress,
            delegate { DialogsManager.HideDialog(busyDialog); },
            delegate(Exception error)
            {
                DialogsManager.HideDialog(busyDialog);
                DialogsManager.ShowDialog(null,
                    new MessageDialog(LanguageManager.Error, error.Message, LanguageManager.Ok));
            });
    }

    public void DeleteEntry(CommunityContentEntry entry)
    {
        if (UserManager.ActiveUser != null)
        {
            DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Get(GetType().Name, 4),
                LanguageManager.Get(GetType().Name, 5), LanguageManager.Yes, LanguageManager.No,
                delegate(MessageDialogButton button)
                {
                    if (button != MessageDialogButton.Button1)
                    {
                        return;
                    }

                    var busyDialog =
                        new CancellableBusyDialog(string.Format(LanguageManager.Get(GetType().Name, 3), entry.Name),
                            false);
                    DialogsManager.ShowDialog(null, busyDialog);
                    CommunityContentManager.Delete(
                        entry.Address,
                        UserManager.ActiveUser.UniqueId,
                        busyDialog.Progress,
                        delegate
                        {
                            DialogsManager.HideDialog(busyDialog);
                            DialogsManager.ShowDialog(null,
                                new MessageDialog(LanguageManager.Get(GetType().Name, 6),
                                    LanguageManager.Get(GetType().Name, 7), LanguageManager.Ok));
                        },
                        delegate(Exception error)
                        {
                            DialogsManager.HideDialog(busyDialog);
                            DialogsManager.ShowDialog(null,
                                new MessageDialog(LanguageManager.Error, error.Message, LanguageManager.Ok));
                        });
                }));
        }
    }

    public string GetFilterDisplayName(object? filter)
    {
        return filter switch
        {
            string s => LanguageManager.Get(nameof(CommunityContentScreen), !string.IsNullOrEmpty(s) ? 8 : 9),
            ExternalContentType type => ExternalContentManager.GetEntryTypeDescription(type),
            _ => throw new InvalidOperationException(LanguageManager.Get(nameof(CommunityContentScreen), 10))
        };
    }

    public string GetOrderDisplayName(Order order)
    {
        return order switch
        {
            Order.ByRank => _isCnLanguageType ? "评分最高" : "ByRank",
            Order.ByTime => _isCnLanguageType ? "最新发布" : "ByTime",
            Order.ByBoutique => _isCnLanguageType ? "精品推荐" : "ByBoutique",
            Order.ByHide => _isCnLanguageType ? "尚未发布" : "ByHide",
            _ => throw new InvalidOperationException(LanguageManager.Get(nameof(CommunityContentScreen), 13))
        };
    }

    public string GetSearchTypeDisplayName(SearchType searchType)
    {
        return searchType switch
        {
            SearchType.ByName => _isCnLanguageType ? "资源名" : "Name",
            SearchType.ByAuthor => _isCnLanguageType ? "用户名" : "User",
            SearchType.ByUserId => _isCnLanguageType ? "用户ID" : "UID",
            _ => "null"
        };
    }
}
