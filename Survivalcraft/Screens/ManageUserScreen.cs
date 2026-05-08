using System.Text.Json.Nodes;
using System.Xml.Linq;
using Engine.Graphics;

namespace Game.Screens;

public class ManageUserScreen : Screen
{
    public enum Filter
    {
        All = 0,
        Blacklisted = 1,
        Admin = 2,
        Inactive = 3,
        Die = 4
    }

    public enum SearchType
    {
        ByUserId = 0,
        ByUserNo = 1,
        ByName = 2,
        ByEmail = 3,
        ByToken = 4,
        ByLoginIP = 5,
        ByLockReason = 6
    }

    private const string _typeName = "ManageUserScreen";

    private readonly ListPanelWidget _contentList;

    private Filter _filter;

    private readonly ButtonWidget _filterButton;

    private readonly LabelWidget _filterLabel;

    private readonly ButtonWidget _lockButton;

    private LinkWidget? _moreLink;

    private bool _order;

    private readonly ButtonWidget _orderButton;

    private readonly ButtonWidget _resetButton;

    private readonly ButtonWidget _searchButton;

    private readonly TextBoxWidget _searchKeyTextBox;

    private SearchType _searchType;

    private readonly ButtonWidget _searchTypeButton;

    public ManageUserScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ManageUserScreen");
        LoadContents(this, node);
        _contentList = Children.Find<ListPanelWidget>("ContentList")!;
        _lockButton = Children.Find<ButtonWidget>("Lock")!;
        _resetButton = Children.Find<ButtonWidget>("Reset")!;
        _orderButton = Children.Find<ButtonWidget>("Order")!;
        _filterLabel = Children.Find<LabelWidget>("Filter")!;
        _filterButton = Children.Find<ButtonWidget>("ChangeFilter")!;
        _searchButton = Children.Find<ButtonWidget>("Search")!;
        _searchTypeButton = Children.Find<ButtonWidget>("SearchType")!;
        _searchKeyTextBox = Children.Find<TextBoxWidget>("SearchKey")!;
        _contentList.ItemWidgetFactory = delegate(object obj)
        {
            if (obj is ComUserInfo listItem)
            {
                var node2 = ContentManager.Get<XElement>("Widgets/BlocksTextureItem");
                var containerWidget = (ContainerWidget)LoadWidget(this, node2, null);
                var rectangleWidget =
                    containerWidget.Children.Find<RectangleWidget>("BlocksTextureItem.Icon")!;
                var labelWidget = containerWidget.Children.Find<LabelWidget>("BlocksTextureItem.Text")!;
                var labelWidget2 = containerWidget.Children.Find<LabelWidget>("BlocksTextureItem.Details")!;
                rectangleWidget.Subtexture = listItem.ImgTexture == null
                    ? ContentManager.Get<Subtexture>("Textures/headimg")
                    : new Subtexture(listItem.ImgTexture, Vector2.Zero, Vector2.One);
                rectangleWidget.TextureLinearFilter = true;
                labelWidget.Text = $"{listItem.Name}   ID:{listItem.Id}   账号:{listItem.UserNo}";
                if (listItem.IsLock == 1)
                {
                    labelWidget2.Text = "锁定时长: " + (int)(listItem.LockDuration / 8.64f) / 10000f + "天";
                    labelWidget2.Text += "  解锁时间: " + GetMsg(listItem.UnlockTime);
                    labelWidget2.Text += "  锁定原因:" + GetMsg(listItem.LockReason);
                }
                else
                {
                    labelWidget2.Text =
                        $"经验:{GetMsg(listItem.Money)}  邮箱:{GetMsg(listItem.Email)} IP地址:{GetMsg(listItem.LoginIP)}";
                }

                labelWidget.Color = Color.White;
                if (listItem.IsLock == 1)
                {
                    labelWidget.Color = Color.LightBlue;
                }
                else if (listItem.IsAdmin == 1)
                {
                    labelWidget.Color = Color.Green;
                }
                else if (listItem.Die == 1)
                {
                    labelWidget.Color = Color.Red;
                }
                else if (listItem.Status == 0)
                {
                    labelWidget.Color = Color.Gray;
                }

                return containerWidget;
            }
            else
            {
                var node2 = ContentManager.Get<XElement>("Widgets/CommunityContentItemMore");
                var containerWidget = (ContainerWidget)LoadWidget(this, node2, null);
                _moreLink = containerWidget.Children.Find<LinkWidget>("CommunityContentItemMore.Link")!;
                _moreLink.Tag = obj as string ?? string.Empty;
                return containerWidget;
            }
        };
        _contentList.ItemClicked += obj =>
        {
            if (obj is not ComUserInfo listItem || _contentList.SelectedItem != listItem)
            {
                return;
            }

            var msg =
                $"用户ID: {listItem.Id}\n用户名: {GetMsg(listItem.UserNo)}\n昵称: {GetMsg(listItem.Name)}\n邮箱{GetMsg(listItem.Email)}\nIP:{GetMsg(listItem.LoginIP)}";
            msg += $"\n用户Token: {GetMsg(listItem.Token)}\n状态: " + (listItem.IsLock == 1
                ? "锁定"
                : listItem.Status == 0
                    ? "未激活"
                    : listItem.Die == 1
                        ? "死鱼"
                        : "正常");
            msg += "\n是否为管理: " + (listItem.IsAdmin == 1 ? "是" : "否") + "  " + "权限等级: " + listItem.Authority;
            msg +=
                $"\n找回密码Token: {GetMsg(listItem.PawToken)}\n称号组: {GetMsg(listItem.MGroup)}\n注册时间: {GetMsg(listItem.RegTime)}\n最后登录时间: {GetMsg(listItem.LastLoginTime)}";
            msg += $"\n当天发送邮件次数: {GetMsg(listItem.EmailCount)}\n邮箱锁定时间: {GetMsg(listItem.EmailTime)}";
            msg += $"\n手机号: {GetMsg(listItem.Moblie)}\n区号: {GetMsg(listItem.AreaCode)}";
            msg += "\n上次锁定时间: " + GetMsg(listItem.LockTime) + "\n锁定原因: " + GetMsg(listItem.LockReason);
            msg += "\n锁定时长: " + (int)(listItem.LockDuration / 8.64f) / 10000f + "天\n解锁时间: " +
                   GetMsg(listItem.UnlockTime);
            var messageDialog = new MessageDialog("详细信息:" + listItem.Name, msg, LanguageControl.Ok);
            DialogsManager.ShowDialog(null, messageDialog);
        };
    }

    public override void Enter(object[] parameters)
    {
        _filter = Filter.All;
        _searchType = SearchType.ByName;
        _order = false;
        UpdateList(string.Empty);
    }

    public override void Update()
    {
        if (_contentList.SelectedItem is ComUserInfo info)
        {
            if (info.IsAdmin == 1)
            {
                _lockButton.IsEnabled = false;
                _resetButton.IsEnabled = false;
            }
            else
            {
                _lockButton.IsEnabled = true;
                _resetButton.IsEnabled = true;
            }

            _lockButton.Text = info.IsLock == 1 ? "解锁" : "锁定";
        }
        else
        {
            _lockButton.IsEnabled = false;
            _resetButton.IsEnabled = false;
        }

        _filterLabel.Text = GetFilterDisplayName(_filter);
        if (_filterButton.IsClicked)
        {
            var filters = EnumUtils.GetEnumValues(typeof(Filter)).Cast<Filter>().ToList();

            DialogsManager.ShowDialog(
                null,
                new ListSelectionDialog(
                    "请选择",
                    filters,
                    60f,
                    item => GetFilterDisplayName((Filter)item),
                    delegate(object result)
                    {
                        _filter = (Filter)result;
                        UpdateList(string.Empty);
                    }
                )
            );
        }

        _searchTypeButton.Text = GetSearchTypeName(_searchType);
        if (_searchTypeButton.IsClicked)
        {
            var searchTypes = EnumUtils.GetEnumValues(typeof(SearchType)).Cast<SearchType>().ToList();
            DialogsManager.ShowDialog(
                null,
                new ListSelectionDialog(
                    "请选择",
                    searchTypes,
                    60f,
                    item => GetSearchTypeName((SearchType)item),
                    delegate(object result) { _searchType = (SearchType)result; }
                )
            );
        }

        if (_searchButton.IsClicked)
        {
            UpdateList(string.Empty);
        }

        if (_lockButton.IsClicked && _contentList.SelectedItem is ComUserInfo userInfo)
        {
            if (userInfo.IsLock == 0)
            {
                DialogsManager.ShowDialog(null, new TextBoxDialog("请输入锁定原因", userInfo.LockReason, 1024,
                    delegate(string reason)
                    {
                        DialogsManager.ShowDialog(null, new TextBoxDialog("请输入锁定时长，单位为天", "1", 1024,
                            delegate(string duration)
                            {
                                if (string.IsNullOrEmpty(reason) || string.IsNullOrEmpty(duration))
                                {
                                    return;
                                }

                                var busyDialog = new CancellableBusyDialog("操作等待中", false);
                                DialogsManager.ShowDialog(null, busyDialog);
                                var sDuration = (int)(double.Parse(duration) * 86400);
                                CommunityContentManager.UpdateLockState(
                                    userInfo.Id,
                                    1,
                                    reason,
                                    sDuration,
                                    busyDialog.Progress,
                                    delegate(byte[] data)
                                    {
                                        DialogsManager.HideDialog(busyDialog);
                                        UpdateList(string.Empty);
                                        if (WebManager.JsonFromBytes(data) is not JsonObject result)
                                        {
                                            return;
                                        }

                                        var msg = result[0]?.ToString() == "200"
                                            ? "成功锁定：" + userInfo.Name
                                            : result[1]?.ToString() ?? string.Empty;
                                        DialogsManager.ShowDialog(
                                            null,
                                            new MessageDialog(
                                                "操作成功",
                                                msg,
                                                LanguageControl.Ok
                                            )
                                        );
                                    },
                                    delegate(Exception e)
                                    {
                                        DialogsManager.HideDialog(busyDialog);
                                        DialogsManager.ShowDialog(
                                            null,
                                            new MessageDialog(
                                                LanguageControl.Error,
                                                e.Message,
                                                LanguageControl.Ok
                                            )
                                        );
                                    });
                            }));
                    }));
            }
            else
            {
                DialogsManager.ShowDialog(null, new MessageDialog("确认解锁？", userInfo.Name, LanguageControl.Ok,
                    LanguageControl.Cancel, delegate(MessageDialogButton button)
                    {
                        if (button != MessageDialogButton.Button1)
                        {
                            return;
                        }

                        var busyDialog = new CancellableBusyDialog("操作等待中", false);
                        DialogsManager.ShowDialog(null, busyDialog);
                        CommunityContentManager.UpdateLockState(
                            userInfo.Id,
                            0,
                            "",
                            0,
                            busyDialog.Progress,
                            delegate(byte[] data)
                            {
                                DialogsManager.HideDialog(busyDialog);
                                UpdateList(string.Empty);
                                if (WebManager.JsonFromBytes(data) is not JsonObject result)
                                {
                                    return;
                                }

                                var msg = result[0]?.ToString() == "200"
                                    ? "成功解锁：" + userInfo.Name
                                    : result[1]?.ToString() ?? string.Empty;
                                DialogsManager.ShowDialog(
                                    null,
                                    new MessageDialog(
                                        "操作成功",
                                        msg,
                                        LanguageControl.Ok
                                    )
                                );
                            },
                            delegate(Exception e)
                            {
                                DialogsManager.HideDialog(busyDialog);
                                DialogsManager.ShowDialog(
                                    null,
                                    new MessageDialog(
                                        LanguageControl.Error,
                                        e.Message,
                                        LanguageControl.Ok
                                    )
                                );
                            });
                    }));
            }
        }

        if (_resetButton.IsClicked && _contentList.SelectedItem is ComUserInfo selectedItem)
        {
            DialogsManager.ShowDialog(null, new MessageDialog("确认重置密码？", selectedItem.Name, LanguageControl.Ok,
                LanguageControl.Cancel, delegate(MessageDialogButton button)
                {
                    if (button != MessageDialogButton.Button1)
                    {
                        return;
                    }

                    var busyDialog = new CancellableBusyDialog("操作等待中", false);
                    DialogsManager.ShowDialog(null, busyDialog);
                    CommunityContentManager.ResetPassword(selectedItem.Id, busyDialog.Progress, delegate(byte[] data)
                    {
                        DialogsManager.HideDialog(busyDialog);
                        if (WebManager.JsonFromBytes(data) is not JsonObject result)
                        {
                            return;
                        }

                        var msg = result[0]?.ToString() == "200"
                            ? "成功重置密码，密码为123456"
                            : result[1]?.ToString() ?? string.Empty;
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                "操作成功",
                                msg,
                                LanguageControl.Ok
                            )
                        );
                    }, delegate(Exception e)
                    {
                        DialogsManager.HideDialog(busyDialog);
                        DialogsManager.ShowDialog(
                            null,
                            new MessageDialog(
                                LanguageControl.Error,
                                e.Message,
                                LanguageControl.Ok
                            )
                        );
                    });
                }));
        }

        if (_orderButton.IsClicked)
        {
            _order = !_order;
            UpdateList(string.Empty);
        }

        if (_moreLink is { IsClicked: true })
        {
            UpdateList((string)_moreLink.Tag);
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    public string GetMsg(object msg)
    {
        return string.IsNullOrEmpty(msg.ToString()) ? string.Empty : msg.ToString() ?? string.Empty;
    }

    public void UpdateList(string cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            _contentList.ClearItems();
            _contentList.ScrollPosition = 0f;
        }

        var busyDialog =
            new CancellableBusyDialog(LanguageControl.Get("CommunityContentScreen", 2), false);
        DialogsManager.ShowDialog(null, busyDialog);
        var order = _order ? 1 : 0;
        CommunityContentManager.UserList(
            cursor,
            _searchKeyTextBox.Text,
            _searchType.ToString(),
            _filter.ToString(),
            order,
            busyDialog.Progress,
            delegate(List<ComUserInfo> list, string nextCursor)
            {
                DialogsManager.HideDialog(busyDialog);
                while (_contentList.Items.Count > 0 &&
                       _contentList.Items[^1] is not ComUserInfo)
                {
                    _contentList.RemoveItemAt(_contentList.Items.Count - 1);
                }

                foreach (var item in list)
                {
                    _contentList.AddItem(item);
                }

                if (list.Count > 0 && !string.IsNullOrEmpty(nextCursor))
                {
                    _contentList.AddItem(nextCursor);
                }
            },
            delegate(Exception error)
            {
                DialogsManager.HideDialog(busyDialog);
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        LanguageControl.Error,
                        error.Message,
                        LanguageControl.Ok
                    )
                );
            }
        );
    }

    public string GetFilterDisplayName(Filter filter)
    {
        return filter switch
        {
            Filter.All => "全部名单",
            Filter.Admin => "管理员名单",
            Filter.Blacklisted => "封禁名单",
            Filter.Inactive => "未激活名单",
            Filter.Die => "死鱼名单",
            _ => ""
        };
    }

    public string GetSearchTypeName(SearchType searchType)
    {
        return searchType switch
        {
            SearchType.ByName => "昵称",
            SearchType.ByEmail => "邮箱",
            SearchType.ByUserId => "用户ID",
            SearchType.ByUserNo => "用户名",
            SearchType.ByToken => "Token",
            SearchType.ByLoginIP => "登录IP",
            SearchType.ByLockReason => "锁定原因",
            _ => ""
        };
    }

    public class ComUserInfo
    {
        public string AreaCode = string.Empty;

        public string Authority = string.Empty;

        public int Die;

        public string Email = string.Empty;

        public int EmailCount;

        public string EmailTime = string.Empty;

        public int ErrCount;

        public string HeadImg = string.Empty;

        public int Id;

        public Texture2D? ImgTexture;

        public int IsAdmin;

        public int IsLock;

        public string LastLoginTime = string.Empty;

        public int LockDuration;

        public string LockReason = string.Empty;

        public string LockTime = string.Empty;

        public string LoginIP = string.Empty;

        public string MGroup = string.Empty;

        public string Moblie = string.Empty;

        public int Money;

        public string Name = string.Empty;

        public string PawToken = string.Empty;

        public string RegTime = string.Empty;

        public int Status = 1;

        public string Token = string.Empty;

        public string UnlockTime = string.Empty;

        public string UserNo = string.Empty;
    }
}
