using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Widgets;

public class NetGroupPanelWidget : CanvasWidget
{
    /// <summary>
    /// 加入队伍的间隔
    /// </summary>
    private const int _addFriendPeriod = 30;

    private readonly ClickableTextRowWidget _create;

    private readonly ClickableTextRowWidget _join;

    private readonly PlayerData _playerData;

    /// <summary>
    /// 是否创建队伍
    /// </summary>
    private bool _createTeam;

    /// <summary>
    /// 是否加入队伍
    /// </summary>
    private bool _joinTeam;

    private double _lastSendTime;

    public NetGroupPanelWidget(PlayerData playerData)
    {
        _playerData = playerData;
        _create = new ClickableTextRowWidget("创建队伍");
        _join = new ClickableTextRowWidget("加入队伍");
        InitPanel();
        Size = new Vector2(220, 120);
    }

    private void InitPanel()
    {
        var verticalPanel = new StackPanelWidget { Direction = LayoutDirection.Vertical };
        AddChildren(verticalPanel);
        verticalPanel.AddChildren(_create);
        verticalPanel.AddChildren(new RectangleWidget
        {
            Size = new Vector2(float.PositiveInfinity, 1),
            FillColor = Color.White
        });
        verticalPanel.AddChildren(_join);
    }

    /// <summary>
    /// 创建队伍
    /// </summary>
    public void CreateTeam()
    {
        _createTeam = true;
    }

    /// <summary>
    /// 加入队伍
    /// </summary>
    public void JoinTeam()
    {
        _joinTeam = true;
    }

    public override void Update()
    {
        if (_create.IsClicked || _createTeam)
        {
            DialogsManager.ShowDialog(
                _playerData.GameWidget.GuiWidget,
                new TextBoxDialog(
                    "输入队伍名称",
                    string.Empty,
                    64,
                    text =>
                    {
                        if (string.IsNullOrEmpty(text))
                        {
                            DialogsManager.Alert("队伍名称不能为空", _playerData.GameWidget.GuiWidget);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(_playerData.GroupKey))
                            {
                                DialogsManager.Alert("你已在队伍中", _playerData.GameWidget.GuiWidget);
                            }
                            else
                            {
                                DialogsManager.Loading("创建中...");
                                if (CommonLib.WorkType == WorkType.Client)
                                {
                                    CommonLib.Net.QueuePackage(new GroupManagePackage(_playerData.PlayerGUID, text,
                                        false));
                                }
                                else
                                {
                                    GroupManagePackage.CreateGroup(true, _playerData.SubsystemPlayers, CommonLib.Net,
                                        _playerData.PlayerGUID, text);
                                }
                            }
                        }
                    },
                    invokeHandlerOnCancel: false
                )
            );
        }

        if (_join.IsClicked || _joinTeam)
        {
            var list = new ListSelectionDialog(
                "选择队伍", _playerData.SubsystemPlayers.ServerGroups.Keys,
                48f,
                obj =>
                {
                    if (_playerData.SubsystemPlayers.ServerGroups.TryGetValue(obj.ToString() ?? string.Empty,
                            out var group))
                    {
                        return group.Name;
                    }

                    return "oops!不存在的队伍";
                },
                obj =>
                {
                    if (_playerData.SubsystemPlayers.ServerGroups.TryGetValue(obj.ToString() ?? string.Empty,
                            out var group))
                    {
                        DialogsManager.Confirm(
                            $"请求加入{group.Name}的队伍?",
                            button =>
                            {
                                if (button != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                var time = (int)(Time.RealTime - _lastSendTime);
                                if (Time.RealTime >= _addFriendPeriod && time <= _addFriendPeriod)
                                {
                                    DialogsManager.Alert(
                                        $"你已经发送过请求了，{_addFriendPeriod - time}s后可再次发送",
                                        _playerData.GameWidget.GuiWidget
                                    );
                                }
                                else
                                {
                                    _lastSendTime = Time.RealTime;
                                    var g = new Guid(obj.ToString() ?? string.Empty);
                                    CommonLib.Net.QueuePackage(new GroupManagePackage(g, _playerData.PlayerGUID, g,
                                        false));
                                    DialogsManager.Alert("加入请求已发送", _playerData.GameWidget.GuiWidget);
                                }
                            },
                            _playerData.GameWidget.GuiWidget
                        );
                    }
                });
            DialogsManager.ShowDialog(_playerData.GameWidget.GuiWidget, list);
        }

        _createTeam = false;
        _joinTeam = false;
    }
}
