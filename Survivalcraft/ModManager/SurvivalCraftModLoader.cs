using Engine.Graphics;

using Game.Network;
using Game.Network.Enums;

namespace Game.ModManager;

public class SurvivalCraftModLoader : ModLoader
{
    public override void ModInitialize()
    {
        ModsManager.RegisterHook("OnCameraChange", this);
        ModsManager.RegisterHook("OnPlayerDead", this);
        ModsManager.RegisterHook("OnModelRendererDrawExtra", this);
        ModsManager.RegisterHook("GetMaxInstancesCount", this);
    }

    public override void OnCameraChange(ComponentPlayer componentPlayer, ComponentGui componentGui)
    {
        var gameWidget = componentPlayer.GameWidget;
        if (gameWidget.ActiveCamera is FppCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<TppCamera>()!;
            componentGui.DisplaySmallMessage(LanguageControl.Get(ComponentGui.TypeName, 9), Color.White, false, false);
        }
        else if (gameWidget.ActiveCamera is TppCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<OrbitCamera>()!;
            componentGui.DisplaySmallMessage(LanguageControl.Get(ComponentGui.TypeName, 10), Color.White, false, false);
        }
        else if (gameWidget.ActiveCamera is OrbitCamera)
        {
            gameWidget.ActiveCamera = gameWidget.FindCamera<FixedCamera>()!;
            componentGui.DisplaySmallMessage(LanguageControl.Get(ComponentGui.TypeName, 11), Color.White, false, false);
        }
        else
        {
            var isAdmin = false;
            if (componentGui.ComponentPlayer is { PlayerData: not null })
            {
                isAdmin = componentGui.ComponentPlayer.PlayerData.ServerManager ||
                          componentGui.ComponentPlayer.PlayerData.ServerMaster;
            }

            if ((componentGui.SubsystemGameInfo.WorldSettings.GameMode == GameMode.Creative || isAdmin) &&
                gameWidget.ActiveCamera is FixedCamera)
            {
                gameWidget.ActiveCamera = gameWidget.FindCamera<DebugCamera>()!;
                componentGui.DisplaySmallMessage(
                    LanguageControl.Get(ComponentGui.TypeName, 19),
                    Color.White,
                    false,
                    false
                );
            }
            else
            {
                gameWidget.ActiveCamera = gameWidget.FindCamera<FppCamera>()!;
                componentGui.DisplaySmallMessage(
                    LanguageControl.Get(ComponentGui.TypeName, 12),
                    Color.White,
                    false,
                    false
                );
            }
        }
    }

    public override bool OnPlayerSpawned(
        PlayerData.SpawnMode spawnMode,
        ComponentPlayer componentPlayer,
        Vector3 position
    )
    {
        return false;
    }

    public override void OnPlayerDead(PlayerData playerData)
    {
#if !SERVER
        playerData.GameWidget.ActiveCamera = playerData.GameWidget.FindCamera<DeathCamera>()!;
#endif

        if (playerData.ComponentPlayer != null)
        {
            var text = playerData.ComponentPlayer.ComponentHealth.CauseOfDeath;
            if (string.IsNullOrEmpty(text))
            {
                text = LanguageControl.Get(PlayerData.TypeName, 12);
            }

#if !SERVER
            var arg = string.Format(LanguageControl.Get(PlayerData.TypeName, 13), text);
            if (playerData.SubsystemGameInfo.WorldSettings.GameMode == GameMode.Cruel)
            {
                playerData.ComponentPlayer.ComponentGui.DisplayLargeMessage(LanguageControl.Get(PlayerData.TypeName, 6),
                    string.Format(LanguageControl.Get(PlayerData.TypeName, 7), arg,
                        LanguageControl.Get("GameMode",
                            playerData.SubsystemGameInfo.WorldSettings.GameMode.ToString())), 30f, 1.5f);
            }
            else if (playerData.SubsystemGameInfo.WorldSettings is
                     { GameMode: GameMode.Adventure, IsAdventureRespawnAllowed: false })
            {
                playerData.ComponentPlayer.ComponentGui.DisplayLargeMessage(LanguageControl.Get(PlayerData.TypeName, 6),
                    string.Format(LanguageControl.Get(PlayerData.TypeName, 8), arg), 30f, 1.5f);
            }
            else
            {
                playerData.ComponentPlayer.ComponentGui.DisplayLargeMessage(LanguageControl.Get(PlayerData.TypeName, 6),
                    string.Format(LanguageControl.Get(PlayerData.TypeName, 9), arg), 30f, 1.5f);
            }
#endif

            if (CommonLib.WorkType == WorkType.Server)
            {
                playerData.SubsystemGameWidgets.AddMessage($"{playerData.Name} <c=red>{text}</c>");
            }
        }

        playerData.Level = MathUtils.Max(MathUtils.Floor(playerData.Level / 2f), 1f);
    }

    public override void OnModelRendererDrawExtra(
        SubsystemModelsRenderer modelsRenderer,
        ComponentModel componentModel,
        Camera camera,
        float? alphaThreshold
    )
    {
        // Player name rendering is handled by ComponentHumanModel.DrawExtras.
    }

    public override int GetMaxInstancesCount()
    {
        return 7;
    }
}
