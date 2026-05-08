using Engine.Graphics;
using Game.NetWork;

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
        playerData.GameWidget.ActiveCamera = playerData.GameWidget.FindCamera<DeathCamera>()!;
        if (playerData.ComponentPlayer != null)
        {
            var text = playerData.ComponentPlayer.ComponentHealth.CauseOfDeath;
            if (string.IsNullOrEmpty(text))
            {
                text = LanguageControl.Get(PlayerData.TypeName, 12);
            }

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
        if (componentModel is not ComponentHumanModel)
        {
            return;
        }

        var componentPlayer = componentModel.Entity.FindComponent<ComponentPlayer>();
        if (componentPlayer == null || camera.GameWidget.PlayerData == componentPlayer.PlayerData)
        {
            return;
        }

        var componentCreature = componentPlayer.ComponentMiner.ComponentCreature;
        var position =
            Vector3.Transform(
                componentCreature.ComponentBody.Position +
                1.02f * Vector3.UnitY * componentCreature.ComponentBody.BoxSize.Y, camera.ViewMatrix);
        if (!(position.Z < 0f))
        {
            return;
        }

        var color = Color.Lerp(Color.White, Color.Transparent,
            MathUtils.Saturate((position.Length() - 4f) / 3f));
        if (color.A <= 8)
        {
            return;
        }

        var right = Vector3.TransformNormal(
            0.005f * Vector3.Normalize(Vector3.Cross(camera.ViewDirection, Vector3.UnitY)),
            camera.ViewMatrix);
        var down = Vector3.TransformNormal(-0.005f * Vector3.UnitY, camera.ViewMatrix);
        var font = LabelWidget.BitmapFont;
        modelsRenderer.PrimitivesRenderer
            .FontBatch(font, 1, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor,
                BlendState.AlphaBlend, SamplerState.LinearClamp).QueueText(
                componentPlayer.PlayerData.Name, position, right, down, color,
                TextAnchor.HorizontalCenter | TextAnchor.Bottom);
    }

    public override int GetMaxInstancesCount()
    {
        return 7;
    }
}
