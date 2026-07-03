using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

/// <summary>
/// 单个玩家条目组件
/// </summary>
public class PlayerItemWidget : CanvasWidget
{
    /// <summary>
    /// 加入队伍的间隔
    /// </summary>
    private const int _addFriendPeriod = 15;

    /// <summary>
    /// + 纹理
    /// </summary>
    private static readonly Subtexture _plus = TextureAtlasManager.GetSubtexture("Textures/Atlas/Plus");

    /// <summary>
    /// 退出组队纹理
    /// </summary>
    private static readonly Subtexture _exitGroup = TextureAtlasManager.GetSubtexture("Textures/Gui/ExitGroup");

    /// <summary>
    /// 睡觉图标
    /// </summary>
    private static readonly Subtexture _sleep = TextureAtlasManager.GetSubtexture("Textures/Atlas/Sleep");

    /// <summary>
    /// 用户管理图标
    /// </summary>
    private static readonly Subtexture _manageUser = TextureAtlasManager.GetSubtexture("Textures/Gui/ManageUser");

    /// <summary>
    /// 箭头图标
    /// </summary>
    private static readonly Subtexture _arrow = TextureAtlasManager.GetSubtexture("Textures/Gui/Arrow");

    /// <summary>
    /// 组件容器
    /// </summary>
    private readonly StackPanelWidget _container = new()
    {
        Direction = LayoutDirection.Horizontal, HorizontalAlignment = WidgetAlignment.Far,
        VerticalAlignment = WidgetAlignment.Stretch
    };

    /// <summary>
    /// 距离标签
    /// </summary>
    private readonly LabelWidget _distanceText = new() { FontScale = 0.7f };

    /// <summary>
    /// 加入队伍按钮
    /// </summary>
    private readonly BitmapButtonWidget _joinGroupBtn = new()
    {
        Size = new Vector2(16f),
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Center,
        NormalSubtexture = _plus,
        ClickedSubtexture = _plus
    };

    private readonly LabelWidget _labelWidget = new() { FontScale = 0.7f };

    /// <summary>
    /// 联机玩家列表组件
    /// </summary>
    private readonly NetPlayerListWidget _listWidget;

    /// <summary>
    /// 用户管理按钮
    /// </summary>
    private readonly BitmapButtonWidget _manageUserBtn = new()
    {
        Size = new Vector2(14f),
        Margin = new Vector2(5, 0),
        HorizontalAlignment = WidgetAlignment.Center,
        VerticalAlignment = WidgetAlignment.Center,
        NormalSubtexture = _manageUser,
        ClickedSubtexture = _manageUser
    };

    /// <summary>
    /// 玩家方位图像
    /// </summary>
    private readonly ImageWidget _playerDirectionImage = new()
    {
        HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center,
        Margin = new Vector2(5, 0)
    };

    /// <summary>
    /// 红色矩形
    /// </summary>
    private readonly RectangleWidget _rectangleRed = new()
        { FillColor = Color.Red, OutlineThickness = 0f, VerticalAlignment = WidgetAlignment.Far };

    /// <summary>
    /// 白色矩形
    /// </summary>
    private readonly RectangleWidget _rectangleWhite = new()
        { FillColor = Color.White, OutlineThickness = 0f, VerticalAlignment = WidgetAlignment.Far };

    /// <summary>
    /// 展示类型
    /// </summary>
    private readonly NetPanelWidget.ShowType _showType;

    /// <summary>
    /// 睡觉图片
    /// </summary>
    private readonly ImageWidget _sleepImage = new()
    {
        HorizontalAlignment = WidgetAlignment.Center, VerticalAlignment = WidgetAlignment.Center,
        Margin = new Vector2(5, 0)
    };

    /// <summary>
    /// 玩家子系统
    /// </summary>
    private readonly SubsystemPlayers _subsystemPlayers;

    private readonly PlayerData _main;

    /// <summary>
    /// 玩家
    /// </summary>
    public PlayerData Player { get; private set; }

    private readonly string[] _optList = ["移动视角到该玩家(再次点击恢复)", "拉黑"];

    /// <summary>
    /// ??
    /// </summary>
    private double _lastSendTime;

    /// <summary>
    /// 是否离开队伍
    /// </summary>
    private bool _leaveTeam;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="playerData"></param>
    /// <param name="main"></param>
    /// <param name="listWidget">列表组件，父容器对象</param>
    /// <param name="showType">展示类型</param>
    public PlayerItemWidget(
        PlayerData playerData,
        PlayerData main,
        NetPlayerListWidget listWidget,
        NetPanelWidget.ShowType showType
    )
    {
        Size = new Vector2(220, 30);

        _listWidget = listWidget;

        _rectangleWhite.Size = new Vector2(220, 1);
        _rectangleRed.Size = new Vector2(220, 1);

        _main = main;
        Player = playerData;
        _playerDirectionImage.SubTexture = _arrow;
        _playerDirectionImage.Size = new Vector2(12f, 14f);
        _playerDirectionImage.ColorTransForm = Color.LightGreen;

        _sleepImage.Size = new Vector2(14f);
        _sleepImage.SubTexture = _sleep;

        _distanceText.Color = Color.Yellow;
        _subsystemPlayers = _main.Project.FindSubsystem<SubsystemPlayers>(true)!;


        _showType = showType;
        switch (showType)
        {
            case NetPanelWidget.ShowType.OnlinePlayers:
                _container.Children.Add(_sleepImage);
                if (_main.ServerManager)
                {
                    _container.Children.Add(_manageUserBtn);
                }

                _rectangleRed.IsVisible = false;
                _joinGroupBtn.NormalSubtexture = _plus;
                _labelWidget.Color = Color.White;
                _distanceText.IsVisible = false;
                _playerDirectionImage.IsVisible = false;
                if (Player.GroupKey == string.Empty)
                {
                    _container.Children.Add(_joinGroupBtn);
                }

                break;
            case NetPanelWidget.ShowType.Team:
                _container.Children.Add(_distanceText);
                _container.Children.Add(_playerDirectionImage);
                _container.Children.Add(_sleepImage);
                _rectangleRed.IsVisible = true;
                _rectangleRed.IsVisible = true;
                if (Player == _main)
                {
                    //退出队伍或解散
                    _joinGroupBtn.NormalSubtexture = _exitGroup;
                    _joinGroupBtn.ClickedSubtexture = _exitGroup;
                    _container.Children.Add(_joinGroupBtn);
                }

                break;
            case NetPanelWidget.ShowType.BlackList:
                _container.Children.Add(_distanceText);
                _container.Children.Add(_manageUserBtn);
                break;
        }

        Children.Add(_rectangleWhite);
        Children.Add(_rectangleRed);
        Children.Add(_labelWidget);
        Children.Add(_container);
    }

    private float GetAngle(Vector3 direction, Vector3 start, Vector3 desc)
    {
        var e = new Vector2(desc.X, desc.Z);
        var s = new Vector2(start.X, start.Z);
        return Vector2.Angle(new Vector2(direction.X, direction.Z), e - s);
    }

    /// <summary>
    /// 离开队伍
    /// </summary>
    public void LeaveTeam()
    {
        _leaveTeam = true;
    }

    public override void Update()
    {
        _labelWidget.Text = Player.Name;
        if (_manageUserBtn.IsClicked)
        {
            switch (_showType)
            {
                case NetPanelWidget.ShowType.OnlinePlayers:
                    var listSelection = new ListSelectionDialog(
                        "操作",
                        new[] { 0, 1 },
                        48f,
                        obj => new LabelWidget { Text = _optList[(int)obj] },
                        obj =>
                        {
                            var p = (int)obj;
                            switch (p)
                            {
                                case 0:
                                    if (_main.GameWidget.ActiveCamera is DebugCamera)
                                    {
                                        _main.GameWidget.ActiveCamera = _main.GameWidget.FindCamera<FppCamera>()!;
                                    }
                                    else
                                    {
                                        var debugCamera = _main.GameWidget.FindCamera<DebugCamera>()!;
                                        _main.GameWidget.ActiveCamera = debugCamera;
                                        if (Player.ComponentPlayer != null)
                                        {
                                            debugCamera.Position = Player.ComponentPlayer.ComponentBody.Position;
                                        }
                                    }

                                    break;
                                case 1:
                                    DialogsManager.Confirm(
                                        $"是否将{Player.Name}加入黑名单?",
                                        btn =>
                                        {
                                            if (btn != MessageDialogButton.Button1)
                                            {
                                                return;
                                            }

                                            _subsystemPlayers.AddBlackList(Player);
                                            Log.Information($"{_main.Name}将{Player.Name}加入黑名单");
                                        },
                                        _main.GameWidget.GuiWidget
                                    );
                                    break;
                            }
                        });
                    DialogsManager.ShowDialog(_main.GameWidget.GuiWidget, listSelection);
                    break;
                case NetPanelWidget.ShowType.Team:
                    //退出或解散队伍
                    if (_main.GroupKey == string.Empty)
                    {
                        DialogsManager.Alert("你不在队伍中!", _main.GameWidget.GuiWidget);
                    }
                    else
                    {
                        if (_subsystemPlayers.ServerGroups.TryGetValue(_main.GroupKey, out var g))
                        {
                            DialogsManager.Confirm(
                                $"是否将[{g.Name}]加入黑名单?",
                                btn =>
                                {
                                    if (btn != MessageDialogButton.Button1)
                                    {
                                        return;
                                    }

                                    _subsystemPlayers.AddBlackList(Player);
                                    Log.Information($"{_main.Name}将{Player.Name}加入黑名单");
                                },
                                _main.GameWidget.GuiWidget
                            );
                        }
                    }

                    break;
                case NetPanelWidget.ShowType.BlackList:
                    if (_subsystemPlayers.BlackPlayerGuidList.ContainsKey(Player.PlayerGUID.ToString()))
                    {
                        DialogsManager.Confirm(
                            $"是否将{Player.Name}移出黑名单?",
                            btn =>
                            {
                                if (btn != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                _subsystemPlayers.BlackPlayerGuidList.Remove(Player.PlayerGUID.ToString());
                                _listWidget.RefreshList();
                                Log.Information($"{_main.Name}将{Player.Name}移出黑名单");
                            }
                        );
                    }

                    break;
            }
        }

        if (_leaveTeam)
        {
            //退出或解散队伍
            if (_main.SubsystemPlayers.ServerGroups.TryGetValue(_main.GroupKey, out var group))
            {
                DialogsManager.Confirm(
                    $"退出{group.Name}的队伍?",
                    btn =>
                    {
                        if (btn != MessageDialogButton.Button1)
                        {
                            return;
                        }

                        var time = (int)(Time.RealTime - _lastSendTime);
                        if (Time.RealTime >= _addFriendPeriod && time <= _addFriendPeriod)
                        {
                            DialogsManager.Alert(
                                $"你已经发送过请求了，{_addFriendPeriod - time}s后可再次发送",
                                _main.GameWidget.GuiWidget
                            );
                        }
                        else
                        {
                            DialogsManager.Loading("退出中...");
                            _lastSendTime = Time.RealTime;
                            if (CommonLib.WorkType == WorkType.Client)
                            {
                                CommonLib.Net.QueuePackage(new GroupManagePackage(
                                    new Guid(_main.GroupKey),
                                    _main.PlayerGUID,
                                    false
                                ));
                            }
                            else
                            {
                                GroupManagePackage.ExitGroup(
                                    CommonLib.WorkType != WorkType.Client,
                                    _main.SubsystemPlayers,
                                    CommonLib.Net,
                                    _main.PlayerGUID,
                                    new Guid(_main.GroupKey)
                                );
                            }
                        }
                    },
                    _main.GameWidget.GuiWidget
                );
            }

            _leaveTeam = false;
        }

        if (Player.ComponentPlayer == null || _main.ComponentPlayer == null)
        {
            _labelWidget.Color = _subsystemPlayers.BlackPlayerGuidList.ContainsKey(Player.PlayerGUID.ToString())
                ? Color.Purple
                : Color.DarkGray;

            return;
        }

        if (_joinGroupBtn.IsClicked)
        {
            if (_main.GroupKey == string.Empty && Player.GroupKey == string.Empty)
            {
                DialogsManager.Alert("请先创建队伍后再邀请加入", _main.GameWidget.GuiWidget);
            }
            else
            {
                var type = Player.GroupKey == string.Empty ? (byte)0 : (byte)1;

                var isInGroup = _main.IsInGroup(Player.PlayerGUID);
                if (isInGroup)
                {
                    type = 2;
                }

                switch (type)
                {
                    case 0:
                        DialogsManager.Confirm(
                            $"邀请{Player.Name}加入队伍?",
                            btn =>
                            {
                                if (btn != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                var time = (int)(Time.RealTime - _lastSendTime);
                                if (Time.RealTime >= _addFriendPeriod && time <= _addFriendPeriod)
                                {
                                    DialogsManager.Alert(
                                        $"你已经发送过请求了，{_addFriendPeriod - time}s后可再次发送",
                                        _main.GameWidget.GuiWidget
                                    );
                                }
                                else
                                {
                                    _lastSendTime = Time.RealTime;
                                    CommonLib.Net.QueuePackage(new GroupManagePackage(
                                        new Guid(_main.GroupKey),
                                        _main.PlayerGUID,
                                        Player.PlayerGUID
                                    ));
                                    DialogsManager.Alert("邀请加入请求已发送", _main.GameWidget.GuiWidget);
                                }
                            },
                            _main.GameWidget.GuiWidget
                        );
                        break;
                    case 1:
                        if (Player.GroupKey == string.Empty)
                        {
                            DialogsManager.Alert("该玩家不在队伍中，不能申请加入", _main.GameWidget.GuiWidget);
                        }
                        else
                        {
                            if (_main.SubsystemPlayers.ServerGroups.TryGetValue(Player.GroupKey, out var group))
                            {
                                DialogsManager.Confirm(
                                    $"请求加入{group.Name}的队伍?",
                                    btn =>
                                    {
                                        if (btn != MessageDialogButton.Button1)
                                        {
                                            return;
                                        }

                                        var time = (int)(Time.RealTime - _lastSendTime);
                                        if (Time.RealTime >= _addFriendPeriod && time <= _addFriendPeriod)
                                        {
                                            DialogsManager.Alert(
                                                $"你已经发送过请求了，{_addFriendPeriod - time}s后可再次发送",
                                                _main.GameWidget.GuiWidget
                                            );
                                        }
                                        else
                                        {
                                            _lastSendTime = Time.RealTime;
                                            CommonLib.Net.QueuePackage(new GroupManagePackage(
                                                new Guid(Player.GroupKey),
                                                _main.PlayerGUID,
                                                Player.PlayerGUID
                                            ));
                                            DialogsManager.Alert("加入请求已发送", _main.GameWidget.GuiWidget);
                                        }
                                    },
                                    _main.GameWidget.GuiWidget
                                );
                            }
                        }

                        break;
                    case 2:
                        if (_main.GroupKey == string.Empty)
                        {
                            DialogsManager.Alert("你不在队伍中", _main.GameWidget.GuiWidget);
                        }
                        else
                        {
                            if (_main.SubsystemPlayers.ServerGroups.TryGetValue(_main.GroupKey, out var group))
                            {
                                DialogsManager.Confirm(
                                    $"退出{group.Name}的队伍?",
                                    btn =>
                                    {
                                        if (btn != MessageDialogButton.Button1)
                                        {
                                            return;
                                        }

                                        var time = (int)(Time.RealTime - _lastSendTime);
                                        if (Time.RealTime >= _addFriendPeriod && time <= _addFriendPeriod)
                                        {
                                            DialogsManager.Alert(
                                                $"你已经发送过请求了，{_addFriendPeriod - time}s后可再次发送",
                                                _main.GameWidget.GuiWidget
                                            );
                                        }
                                        else
                                        {
                                            DialogsManager.Loading("退出中...");
                                            _lastSendTime = Time.RealTime;
                                            if (CommonLib.WorkType == WorkType.Client)
                                            {
                                                CommonLib.Net.QueuePackage(new GroupManagePackage(
                                                    new Guid(_main.GroupKey),
                                                    _main.PlayerGUID,
                                                    false)
                                                );
                                            }
                                            else
                                            {
                                                GroupManagePackage.ExitGroup(
                                                    CommonLib.WorkType != WorkType.Client,
                                                    _main.SubsystemPlayers,
                                                    CommonLib.Net, _main.PlayerGUID,
                                                    new Guid(_main.GroupKey)
                                                );
                                            }
                                        }
                                    });
                            }
                        }

                        break;
                }
            }
        }

        _sleepImage.IsVisible = Player.ComponentPlayer.ComponentSleep.IsSleeping;
        if (_sleepImage.IsVisible)
        {
            _sleepImage.Padding = new Vector2(2);
            _sleepImage.IsVisible = true;
            _sleepImage.Background = Color.White;
            _distanceText.IsVisible = false;
        }
        else
        {
            switch (_showType)
            {
                case NetPanelWidget.ShowType.OnlinePlayers:
                    _labelWidget.Color = Color.White;
                    if (Player.GroupKey != string.Empty && Player.GroupKey == _main.GroupKey)
                    {
                        _labelWidget.Color = Color.Green;
                    }

                    break;
                case NetPanelWidget.ShowType.Team:
                    var dis = Vector2.Distance(_main.ComponentPlayer.ComponentBody.Position.XZ,
                        Player.ComponentPlayer.ComponentBody.Position.XZ);
                    var angle = GetAngle(_main.ComponentPlayer.GameWidget.ActiveCamera.ViewDirection,
                        _main.ComponentPlayer.ComponentBody.Position, Player.ComponentPlayer.ComponentBody.Position);
                    if (Player == _main)
                    {
                        _labelWidget.Color = Color.Orange;
                        _playerDirectionImage.IsVisible = false;
                        _distanceText.IsVisible = false;
                    }
                    else
                    {
                        _labelWidget.Color = Color.Green;
                        _playerDirectionImage.IsVisible = true;
                        _playerDirectionImage.RotateAngle = angle;
                        _distanceText.IsVisible = true;
                        _distanceText.Text = $"{dis:0.0}m";
                    }

                    _rectangleRed.Size = new Vector2(220f * Player.ComponentPlayer.ComponentHealth.Health, 1f);
                    break;
                case NetPanelWidget.ShowType.BlackList:
                {
                    _labelWidget.Color = Color.Purple;
                }
                    break;
            }
        }
    }
}
