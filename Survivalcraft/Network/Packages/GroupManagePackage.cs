using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class GroupManagePackage : IPackage
{
    public enum CommandType
    {
        CreateGroup,
        RequestJoinGroup,
        InviteJoinGroup,
        JoinGroup,
        ExitGroup,
        RenameGroup
    }

    private CommandType _command;

    private Guid _fromPlayer;

    private Guid _groupKey;

    private string _groupName = string.Empty;

    private bool _result;

    private Guid _toPlayer;

    public byte ID => (byte)PackageType.GroupManage;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public GroupManagePackage()
    {
    }

    public GroupManagePackage(Guid from, string name, bool r)
    {
        _command = CommandType.CreateGroup;
        _fromPlayer = from;
        _result = r;
        _groupName = name;
    }

    /// <summary>
    /// 申请加入队伍
    /// </summary>
    /// <param name="groupKey"></param>
    /// <param name="from"></param>
    /// <param name="joinOrExit">true加入 false退出</param>
    public GroupManagePackage(Guid groupKey, Guid from, bool joinOrExit)
    {
        _command = joinOrExit ? CommandType.JoinGroup : CommandType.ExitGroup;
        _fromPlayer = from;
        this._groupKey = groupKey;
    }

    /// <summary>
    /// 邀请加入队伍
    /// </summary>
    /// <param name="groupKey">队伍ID</param>
    /// <param name="from">邀请人ID</param>
    /// <param name="to">被邀请人ID</param>
    /// <param name="isInvite">true 邀请 false 申请</param>
    public GroupManagePackage(Guid groupKey, Guid from, Guid to, bool isInvite = true)
    {
        _command = isInvite ? CommandType.InviteJoinGroup : CommandType.RequestJoinGroup;
        _fromPlayer = from;
        _toPlayer = to;
        this._groupKey = groupKey;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_command);
        switch (_command)
        {
            case CommandType.CreateGroup:
                writer.Write(_fromPlayer);
                writer.Write(_result);
                writer.Write(_groupName);
                break;
            case CommandType.RequestJoinGroup:
            case CommandType.InviteJoinGroup:
                writer.Write(_toPlayer);
                writer.Write(_fromPlayer);
                writer.Write(_groupKey);
                break;
            case CommandType.JoinGroup:
            case CommandType.ExitGroup:
                writer.Write(_fromPlayer);
                writer.Write(_groupKey);
                break;
            case CommandType.RenameGroup:
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _command = reader.ReadEnum<CommandType>();
        switch (_command)
        {
            case CommandType.CreateGroup:
                _fromPlayer = reader.ReadGuid();
                _result = reader.ReadBoolean();
                _groupName = reader.ReadString();
                break;
            case CommandType.RequestJoinGroup:
            case CommandType.InviteJoinGroup:
                _toPlayer = reader.ReadGuid();
                _fromPlayer = reader.ReadGuid();
                _groupKey = reader.ReadGuid();
                break;
            case CommandType.JoinGroup:
            case CommandType.ExitGroup:
                _fromPlayer = reader.ReadGuid();
                _groupKey = reader.ReadGuid();
                break;
            case CommandType.RenameGroup:
                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;

        switch (_command)
        {
            case CommandType.CreateGroup:
                CreateGroup(isServer, subsystemPlayers, netNode, _fromPlayer, _groupName);
                break;
            case CommandType.RequestJoinGroup:
                if (subsystemPlayers.ServerGroups.TryGetValue(_groupKey.ToString(), out _))
                {
                    var fromPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == _fromPlayer);
                    var toPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == _toPlayer);
                    if (fromPlayerData != null && toPlayerData is { IsMainPlayer: true })
                    {
                        DialogsManager.Confirm(
                            $"{fromPlayerData.Name}申请加入你的队伍，是否同意?",
                            btn =>
                            {
                                if (btn != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                //同意
                                if (isServer)
                                {
                                    JoinGroup(isServer, subsystemPlayers, netNode, fromPlayerData.PlayerGUID,
                                        _groupKey);
                                }
                                else
                                {
                                    toPlayerData.GameWidget.NetPanelWidget?.RefreshView();
                                    netNode.QueuePackage(new GroupManagePackage(_groupKey,
                                        fromPlayerData.PlayerGUID, true));
                                }
                            },
                            toPlayerData.GameWidget.GuiWidget
                        );
                    }

                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                }

                break;
            case CommandType.InviteJoinGroup:
                if (subsystemPlayers.ServerGroups.TryGetValue(_groupKey.ToString(), out var fromGroup))
                {
                    var fromPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == _fromPlayer);
                    var toPlayerData = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == _toPlayer);
                    if (fromPlayerData != null && toPlayerData is { IsMainPlayer: true })
                    {
                        DialogsManager.Confirm(
                            $"{fromPlayerData.Name}想邀请你加入队伍{fromGroup.Name}，是否同意?",
                            btn =>
                            {
                                if (btn != MessageDialogButton.Button1)
                                {
                                    return;
                                }

                                //同意
                                if (isServer)
                                {
                                    JoinGroup(isServer, subsystemPlayers, netNode, _toPlayer, _groupKey);
                                }
                                else
                                {
                                    toPlayerData.GameWidget.NetPanelWidget?.RefreshView();
                                    netNode.QueuePackage(new GroupManagePackage(_groupKey, _toPlayer, true));
                                }
                            },
                            toPlayerData.GameWidget.GuiWidget
                        );
                    }

                    if (isServer)
                    {
                        Except = From;
                        netNode.QueuePackage(this);
                    }
                }

                break;
            case CommandType.JoinGroup:
                JoinGroup(isServer, subsystemPlayers, netNode, _fromPlayer, _groupKey);
                break;
            case CommandType.ExitGroup:
                ExitGroup(isServer, subsystemPlayers, netNode, _fromPlayer, _groupKey);
                break;
            case CommandType.RenameGroup:
                break;
        }
    }

    public static void CreateGroup(bool isServer, SubsystemPlayers subsystemPlayers, NetNode netNode, Guid fromPlayer,
        string groupName)
    {
        var pl = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == fromPlayer);
        var flag = pl is { IsMainPlayer: true };
        if (flag)
        {
            DialogsManager.HideLoadingDialogs();
        }

        if (pl != null)
        {
            if (pl.GroupKey != string.Empty)
            {
                const string msg = "你已在队伍中!";
                if (isServer)
                {
                    netNode.QueuePackage(new GroupManagePackage(fromPlayer, groupName, false) { To = pl.Client });
                }

                if (flag)
                {
                    DialogsManager.Alert(msg, pl.GameWidget.GuiWidget);
                }
            }
            else
            {
                const string msg = "创建队伍成功";
                subsystemPlayers.CreateGroup(pl, groupName);
                if (isServer)
                {
                    netNode.QueuePackage(new GroupManagePackage(fromPlayer, groupName, true));
                }

                if (flag)
                {
                    DialogsManager.Alert(msg, pl.GameWidget.GuiWidget);
                }
            }
        }

        if (flag)
            //刷新列表
        {
            pl!.GameWidget.NetPanelWidget?.RefreshView();
        }
    }

    public static void ExitGroup(bool isServer, SubsystemPlayers subsystemPlayers, NetNode netNode, Guid fromPlayer,
        Guid groupKey)
    {
        var pl = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == fromPlayer);
        var flag = pl is { IsMainPlayer: true };
        if (flag)
        {
            DialogsManager.HideLoadingDialogs();
        }

        if (subsystemPlayers.ServerGroups.TryGetValue(groupKey.ToString(), out var group))
        {
            var notifyList = new List<Guid>();
            notifyList.AddRange(group.Members);
            if (fromPlayer == groupKey)
            {
                //队长退出队伍
                foreach (var g in group.Members)
                {
                    var pl2 = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == g);
                    pl2?.GroupKey = string.Empty;
                }

                subsystemPlayers.ServerGroups.Remove(groupKey.ToString());
            }
            else
            {
                //队员退出队伍
                if (pl != null)
                {
                    pl.GroupKey = string.Empty;
                    group.Members.Remove(fromPlayer);
                }
            }

            if (isServer)
            {
                netNode.QueuePackage(new GroupManagePackage(groupKey, fromPlayer, false));
            }

            if (flag)
            {
                DialogsManager.Alert("退出队伍成功", pl!.GameWidget.GuiWidget);
            }

            foreach (var unused in notifyList)
            {
                var pl2 = subsystemPlayers.PlayersData.Find(p => p.IsMainPlayer);
                //刷新列表
                pl2?.GameWidget.NetPanelWidget?.RefreshView();
            }
        }
        else
        {
            var msg = "不存在队伍" + groupKey;
            if (flag)
            {
                DialogsManager.Alert(msg, pl!.GameWidget.GuiWidget);
            }

            if (isServer && pl != null)
            {
                netNode.QueuePackage(new GroupManagePackage(groupKey, fromPlayer, false) { To = pl.Client });
            }
        }
    }

    public static void JoinGroup(bool isServer, SubsystemPlayers subsystemPlayers, NetNode netNode, Guid fromPlayer,
        Guid groupKey)
    {
        var pl = subsystemPlayers.PlayersData.Find(p => p.PlayerGUID == fromPlayer);
        var flag = pl is { IsMainPlayer: true };
        if (flag)
        {
            DialogsManager.HideLoadingDialogs();
        }

        if (subsystemPlayers.ServerGroups.TryGetValue(groupKey.ToString(), out var group))
        {
            if (group.Members.Contains(fromPlayer))
            {
                var msg = "你已在队伍[" + group.Name + "]中";
                if (flag)
                {
                    DialogsManager.Alert(msg, pl!.GameWidget.GuiWidget);
                }

                if (isServer && pl != null)
                {
                    netNode.QueuePackage(new GroupManagePackage(groupKey, fromPlayer, true) { To = pl.Client });
                }
            }
            else
            {
                group.Members.Add(fromPlayer);
                pl?.GroupKey = groupKey.ToString();
                var msg = "你已成功加入队伍[" + group.Name + "]中";
                if (isServer)
                {
                    netNode.QueuePackage(new GroupManagePackage(groupKey, fromPlayer, true));
                }

                if (flag)
                {
                    DialogsManager.Alert(msg, pl!.GameWidget.GuiWidget);
                }

                foreach (var unused in group.Members)
                {
                    var pl2 = subsystemPlayers.PlayersData.Find(p => p.IsMainPlayer);
                    //刷新列表
                    pl2?.GameWidget.NetPanelWidget?.RefreshView();
                }
            }
        }
        else
        {
            var msg = "不存在队伍" + groupKey;
            if (flag)
            {
                DialogsManager.Alert(msg, pl!.GameWidget.GuiWidget);
            }

            if (isServer && pl != null)
            {
                netNode.QueuePackage(new GroupManagePackage(groupKey, fromPlayer, true) { To = pl.Client });
            }
        }
    }
}
