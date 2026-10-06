namespace Game.Automation;

/// <summary>Read-only observation of the actual locally controlled character.</summary>
public static class AutomationGameplayContext
{
    public static object Capture()
    {
        var project = GameManager.Project;
        var player = project?.FindSubsystem<SubsystemPlayers>()?.ComponentPlayers
            .FirstOrDefault(candidate => candidate.IsLocallyControlled);
        if (player == null)
        {
            return new { Ready = false, Reason = "No local character is available." };
        }

        var camera = player.GameWidget.ActiveCamera;
        var inventory = player.ComponentMiner.Inventory;
        var position = player.ComponentBody.Position;
        var chunk = project!.FindSubsystem<SubsystemTerrain>()?.Terrain.GetChunkAtCell(
            Terrain.ToCell(position.X), Terrain.ToCell(position.Z), false);
        var hit = player.ComponentMiner.Raycast(new Ray3(camera.ViewPosition, camera.ViewDirection), RaycastMode.Digging);
        var target = hit is TerrainRaycastResult terrain
            ? new
            {
                terrain.CellFace.X,
                terrain.CellFace.Y,
                terrain.CellFace.Z,
                terrain.CellFace.Face,
                terrain.Value,
                terrain.Distance
            }
            : null;
        return new
        {
            Ready = player.PlayerData.IsReadyForPlaying,
            WindowActive = Window.IsActive,
            Screen = ScreensManager.GetCurrentScreenName(),
            Transitioning = ScreensManager.IsAnimating,
            Paused = project.FindSubsystem<SubsystemTime>()?.GameTimeFactor == 0f,
            ChunkState = chunk?.MainThreadState.ToString(),
            Dialogs = DialogsManager.ReadOnlyDialogs.Select(dialog => dialog.GetType().Name).ToArray(),
            Position = Coordinates(player.ComponentBody.Position),
            Velocity = Coordinates(player.ComponentBody.Velocity),
            CameraPosition = Coordinates(camera.ViewPosition),
            CameraType = camera.GetType().Name,
            YawDegrees = MathUtils.RadToDeg(MathF.Atan2(camera.ViewDirection.X, camera.ViewDirection.Z)),
            PitchDegrees = MathUtils.RadToDeg(MathF.Asin(Math.Clamp(camera.ViewDirection.Y, -1f, 1f))),
            CameraDirection = Coordinates(camera.ViewDirection),
            Health = player.ComponentHealth.Health,
            Sleeping = player.ComponentSleep.SleepFactor > 0f,
            Flying = player.ComponentLocomotion.IsCreativeFlyEnabled,
            Immersion = player.ComponentBody.ImmersionFactor,
            Grounded = player.ComponentBody.StandingOnValue != null,
            ModalPanel = player.ComponentGui.ModalPanelWidget?.GetType().Name,
            UiInput = new
            {
                player.GameWidget.Input.IsMouseCursorVisible,
                MouseVisible = Engine.Input.Mouse.IsMouseVisible,
                MousePosition = Engine.Input.Mouse.MousePosition is { } point ? new { point.X, point.Y } : null,
                DragActive = player.GameWidget.Input.Drag.HasValue,
                PressActive = player.GameWidget.Input.Press.HasValue
            },
            Target = target,
            HitKind = hit?.GetType().Name,
            inventory.ActiveSlotIndex,
            TotalSlotsCount = inventory.SlotsCount,
            HotbarSlotsCount = (inventory as IPagedInventory)?.HotbarSlotsCount,
            inventory.VisibleSlotsCount,
            Slots = Enumerable.Range(0, inventory is ComponentCreativeInventory
                ? Math.Min(inventory.SlotsCount, PlayerInventoryLayout.StorageSlotsCount)
                : inventory.SlotsCount).Select(index => new
                {
                    Index = index,
                    Value = inventory.GetSlotValue(index),
                    Count = inventory.GetSlotCount(index)
                }).ToArray()
        };
    }

    private static object Coordinates(Vector3 value) => new { value.X, value.Y, value.Z };
}
