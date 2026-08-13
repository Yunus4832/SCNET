namespace Game.Network.Packages.Handlers;

public sealed class PlayerDataPackageHandler : PackageHandlerBase<PlayerDataPackage>
{
    public override void Handle(PlayerDataPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(PlayerDataPackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        PlayerData? playerData;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        switch (package.Type)
        {
            case PlayerDataPackage.DataType.Create:
                if (!isServer)
                {
                    break;
                }

                playerData = new PlayerData(project);
                if (package.Vd != null)
                {
                    playerData.Load(package.Vd);
                }

                subsystemPlayers.AddPlayerData(playerData);
                playerData.Name = PlayerData.CreateNewName(playerData.Name);
                netNode.QueuePackage(new PlayerListPackage(subsystemPlayers));
                break;
            case PlayerDataPackage.DataType.Modify:
                if (isServer)
                {
                    break;
                }

                var playerData2 = subsystemPlayers.FindPlayerData(p => p.PlayerGUID == package.PlayerGuid);
                if (playerData2 != null)
                {
                    playerData2.Name = package.PlayerName;
                    playerData2.CharacterSkinName = package.SkinName;
                    playerData2.PlayerClass = package.PlayerClass;
                }

                break;
            case PlayerDataPackage.DataType.Delete:
                netNode.RemoveClient(package.From);
                break;
            case PlayerDataPackage.DataType.SetUpdateLocation:
                var player = subsystemPlayers.PlayersData.Find(x => x.Client == package.From);
                if (player != null && NetworkTerrainPolicy.TryClampClientUpdateLocation(
                        package.UpdateLocation,
                        SettingsManager.Current.MaxClientVisibilityRange,
                        out var updateLocation
                    )
                   )
                {
                    var updater = project.FindSubsystem<SubsystemTerrain>(true)!.TerrainUpdater;
                    updater.SetLastChunksUpdateCenter(player.PlayerIndex, updateLocation.LastChunksUpdateCenter);
                    updater.SetUpdateLocation(
                        player.PlayerIndex,
                        updateLocation.Center,
                        updateLocation.VisibilityDistance,
                        updateLocation.ContentDistance
                    );
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
