using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class GroupManagePackage : IPackage
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

    public CommandType Command;

    public Guid FromPlayer;

    public Guid GroupKey;

    public string GroupName = string.Empty;

    public bool Result;

    public Guid ToPlayer;

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
        Command = CommandType.CreateGroup;
        FromPlayer = from;
        Result = r;
        GroupName = name;
    }

    /// <summary>
    /// 申请加入队伍
    /// </summary>
    /// <param name="groupKey"></param>
    /// <param name="from"></param>
    /// <param name="joinOrExit">true加入 false退出</param>
    public GroupManagePackage(Guid groupKey, Guid from, bool joinOrExit)
    {
        Command = joinOrExit ? CommandType.JoinGroup : CommandType.ExitGroup;
        FromPlayer = from;
        this.GroupKey = groupKey;
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
        Command = isInvite ? CommandType.InviteJoinGroup : CommandType.RequestJoinGroup;
        FromPlayer = from;
        ToPlayer = to;
        this.GroupKey = groupKey;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(Command);
        switch (Command)
        {
            case CommandType.CreateGroup:
                writer.Write(FromPlayer);
                writer.Write(Result);
                writer.Write(GroupName);
                break;
            case CommandType.RequestJoinGroup:
            case CommandType.InviteJoinGroup:
                writer.Write(ToPlayer);
                writer.Write(FromPlayer);
                writer.Write(GroupKey);
                break;
            case CommandType.JoinGroup:
            case CommandType.ExitGroup:
                writer.Write(FromPlayer);
                writer.Write(GroupKey);
                break;
            case CommandType.RenameGroup:
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Command = reader.ReadEnum<CommandType>();
        switch (Command)
        {
            case CommandType.CreateGroup:
                FromPlayer = reader.ReadGuid();
                Result = reader.ReadBoolean();
                GroupName = reader.ReadString();
                break;
            case CommandType.RequestJoinGroup:
            case CommandType.InviteJoinGroup:
                ToPlayer = reader.ReadGuid();
                FromPlayer = reader.ReadGuid();
                GroupKey = reader.ReadGuid();
                break;
            case CommandType.JoinGroup:
            case CommandType.ExitGroup:
                FromPlayer = reader.ReadGuid();
                GroupKey = reader.ReadGuid();
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
