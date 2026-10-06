namespace Game.Network.Packages.Handlers;

public sealed class PlayerDataPackageHandler : PackageHandlerBase<PlayerDataPackage>
{
    internal static bool CanCreatePlayer(Client? sender, IEnumerable<Guid> existingPlayers) =>
        sender is not null && sender.GUID != Guid.Empty && !existingPlayers.Contains(sender.GUID);

    internal static bool AcceptsDirection(PlayerDataPackage.DataType type, bool isServer) =>
        isServer
            ? type is PlayerDataPackage.DataType.Create or PlayerDataPackage.DataType.Delete or
                PlayerDataPackage.DataType.SetUpdateLocation
            : type is PlayerDataPackage.DataType.Modify or PlayerDataPackage.DataType.CloseTime or
                PlayerDataPackage.DataType.Bugle or PlayerDataPackage.DataType.Count;

    public override void Handle(PlayerDataPackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(PlayerDataPackage)}");
            return;
        }

        if (GameManager.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        PlayerData? playerData;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        switch (package.Type)
        {
            case PlayerDataPackage.DataType.Create:
                if (!CanCreatePlayer(context.Sender, subsystemPlayers.PlayersData.Select(player => player.PlayerGUID)) ||
                    !Enum.IsDefined(package.PlayerClass) || string.IsNullOrWhiteSpace(package.SkinName))
                {
                    break;
                }

                var name = PlayerData.SanitizeName(package.PlayerName.Trim());
                if (!PlayerData.VerifyName(name) || subsystemPlayers.PlayersData.Any(player => player.Name == name))
                {
                    break;
                }

                playerData = new PlayerData(project)
                {
                    PlayerGUID = context.Sender!.GUID,
                    Name = name,
                    CharacterSkinName = package.SkinName,
                    PlayerClass = package.PlayerClass
                };
                subsystemPlayers.AddPlayerData(playerData);
                netNode.QueuePackage(new PlayerListPackage(subsystemPlayers), PackageAudience.Global);
                break;
            case PlayerDataPackage.DataType.Modify:
                var playerData2 = subsystemPlayers.FindPlayerData(p => p.PlayerGUID == package.PlayerGuid);
                if (playerData2 != null)
                {
                    playerData2.Name = package.PlayerName;
                    playerData2.CharacterSkinName = package.SkinName;
                    playerData2.PlayerClass = package.PlayerClass;
                }

                break;
            case PlayerDataPackage.DataType.Delete:
                netNode.RemoveClient(context.Sender);
                break;
            case PlayerDataPackage.DataType.SetUpdateLocation:
                var player = subsystemPlayers.PlayersData.Find(x => ReferenceEquals(x.Client, context.Sender));
                if (player == null)
                {
                    Log.Warning(
                        $"Ignored terrain update location without matching player: " +
                        $"client={context.Sender?.ID.ToString() ?? "null"}, center={package.UpdateLocation.Center}.");
                    break;
                }

                if (context.Sender is null || !project.FindSubsystem<SubsystemNetworkInterest>(true)!
                        .SetRequestedLocation(context.Sender, player, package.UpdateLocation))
                {
                    Log.Warning(
                        $"Rejected invalid terrain update location: client={context.Sender?.ID.ToString() ?? "null"}, " +
                        $"center={package.UpdateLocation.Center}.");
                }

                break;
            case PlayerDataPackage.DataType.CloseTime:
                var p3 = project.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (p3 != null)
                {
                    p3.ComponentGui.CloseTime = package.Visibility;
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            "服务器关闭提醒", package.PlayerName,
                            LanguageManager.Yes,
                            LanguageManager.No,
                            _ => { DialogsManager.HideAllDialogs(); }
                        )
                    );
                }

                break;
            case PlayerDataPackage.DataType.Bugle:
                var mainPlayer = project.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (mainPlayer == null)
                {
                    break;
                }

                if (package.PlayerGuid == Guid.Empty ||
                    (package.PlayerGuid != Guid.Empty &&
                     mainPlayer.PlayerData.PlayerGUID == package.PlayerGuid))
                {
                    DialogsManager.HideAllDialogs();
                    mainPlayer.ComponentHealth.IsInvulnerable = true;
                    package.BugleContent = package.BugleContent.Replace("[n]", "\n").Replace("[e]", " ");
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            package.BugleTitle,
                            package.BugleContent,
                            LanguageManager.Ok,
                            string.Empty,
                            _ =>
                            {
                                DialogsManager.HideAllDialogs();
                                mainPlayer.ComponentHealth.IsInvulnerable = false;
                            }
                        )
                    );
                }

                break;
            case PlayerDataPackage.DataType.Count:
                var mainPlayer2 = project.FindSubsystem<SubsystemPlayers>(true)!.MainPlayer;
                if (mainPlayer2 == null)
                {
                    break;
                }

                var clientPlayerCount = project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData.Count;
                Log.Information($"隐身测试，服务端人数：{package.PlayerCount}; 客户端人数：{clientPlayerCount}");
                if (clientPlayerCount != package.PlayerCount)
                {
                    ScreensManager.SwitchScreen("NetPlay");
                    GameManager.DisposeProject();
                    CommonLib.Net.Stop();
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            "连接异常",
                            "检测到玩家人数异常，请重新连接服务器",
                            LanguageManager.Ok
                        )
                    );
                }

                break;
        }
    }
}
