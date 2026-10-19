using Engine.Core;

using EntitySystem.Core;

using Game;
using Game.Components;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Terrains;

namespace TerritoryStoneMod;

public static class TerritoryProtection
{
    public static void Register(IModContext context)
    {
        var lastDigDenialTime = double.NegativeInfinity;
        context.Gameplay.OnMinerDigging(digging =>
        {
            var point = digging.RaycastResult.CellFace.Point;
            var territory = Find(digging.Miner.Project, point.X, point.Z);
            if (territory != null)
            {
                var player = digging.Miner.ComponentPlayer?.PlayerData;
                var ownerOnly = Terrain.ExtractContents(digging.CellValue) == CurrentModRuntime.Value!.RequireWorldCatalog().GetBlock(TerritoryBlock.Id).BlockIndex &&
                                !AllowsOwner(territory, player);
                if (!CanDig(territory, player) || ownerOnly)
                {
                    digging.Cancel = true;
                    if (Time.FrameStartTime - lastDigDenialTime >= 2 &&
                        Notify(digging.Miner, ownerOnly ? "StoneOwnerOnly" : "DiggingRestricted", territory))
                    {
                        lastDigDenialTime = Time.FrameStartTime;
                    }
                }
            }
        });
        context.BlockBehaviors.OnBlockPlaced(placing =>
        {
            var subsystem = placing.Miner.Project.FindSubsystem<TerritorySubsystem>();
            if (subsystem == null)
            {
                return;
            }

            var territory = subsystem.Store.Find(placing.X, placing.Z);
            if (territory != null && !CanPlace(territory, placing.Miner.ComponentPlayer?.PlayerData))
            {
                placing.Cancel = true;
                Notify(placing.Miner, "PlacingRestricted", territory);
                return;
            }

            if (Terrain.ExtractContents(placing.PlacementData.Value) != CurrentModRuntime.Value!.RequireWorldCatalog().GetBlock(TerritoryBlock.Id).BlockIndex)
            {
                return;
            }

            var owner = placing.Miner.ComponentPlayer?.PlayerData.PlayerGUID ?? Guid.Empty;
            var point = new Point3(placing.X, placing.Y, placing.Z);
            if (!subsystem.Store.CanCreate(owner, point))
            {
                placing.Cancel = true;
                Notify(placing.Miner, "ClaimUnavailable");
                return;
            }

        });
        context.BlockBehaviors.OnBlockAdded(added =>
        {
            if (CommonLib.WorkType == WorkType.Client || Terrain.ExtractContents(added.Value) != CurrentModRuntime.Value!.RequireWorldCatalog().GetBlock(TerritoryBlock.Id).BlockIndex)
            {
                return;
            }

            var owner = added.Miner?.ComponentPlayer?.PlayerData.PlayerGUID ?? Guid.Empty;
            var subsystem = added.Terrain.Project.FindSubsystem<TerritorySubsystem>();
            if (subsystem != null && subsystem.Store.TryCreate(owner, new Point3(added.X, added.Y, added.Z)))
            {
                subsystem.Store.TryGet(owner, out var territory);
                subsystem.PublishUpdate(territory!);
            }
        });
        context.BlockBehaviors.OnBlockRemoved(removed =>
        {
            if (CommonLib.WorkType != WorkType.Client && Terrain.ExtractContents(removed.Value) == CurrentModRuntime.Value!.RequireWorldCatalog().GetBlock(TerritoryBlock.Id).BlockIndex)
            {
                var subsystem = removed.Terrain.Project.FindSubsystem<TerritorySubsystem>();
                var territory = subsystem?.Store.Find(removed.X, removed.Z);
                if (territory != null && subsystem!.Store.RemoveStone(new Point3(removed.X, removed.Y, removed.Z)))
                {
                    subsystem.PublishRemoval(territory.Owner);
                }
            }
        });
        context.Gameplay.OnTerrainCellChanging(changing =>
        {
            var player = changing.Miner?.ComponentPlayer?.PlayerData;
            var territory = Find(changing.Terrain.Project, changing.X, changing.Z);
            if (player == null || territory == null)
            {
                return;
            }
            var oldContents = Terrain.ExtractContents(changing.OldValue);
            var newContents = Terrain.ExtractContents(changing.NewValue);
            changing.Cancel |= oldContents != 0 && newContents == 0 && !CanDig(territory, player) ||
                oldContents == 0 && newContents != 0 && !CanPlace(territory, player);
            changing.Cancel |= oldContents == CurrentModRuntime.Value!.RequireWorldCatalog()
                .GetBlock(TerritoryBlock.Id).BlockIndex && !AllowsOwner(territory, player);
        });
        var lastBoundaryDenialTime = double.NegativeInfinity;
        void OnBoundaryCollision(ComponentBody body, Territory territory, bool exiting)
        {
            if (Time.FrameStartTime - lastBoundaryDenialTime >= 2 &&
                BodyPlayer(body)?.PlayerData.IsMainPlayer == true && RunMode.Value is RunModeType.Gui)
            {
                Notify(BodyPlayer(body)!.ComponentMiner, exiting ? "ExitRestricted" : "EntryRestricted",
                    territory);
                lastBoundaryDenialTime = Time.FrameStartTime;
            }
        }

        context.Gameplay.OnTerrainCollisionBoxes(collision => AddBoundaryCollisions(collision, OnBoundaryCollision));
    }

    public static bool CanDig(Territory territory, PlayerData? player) => player == null ||
        AllowsOwner(territory, player) || territory.Settings.AllowDigging ||
        (territory.PermissionsFor(player.PlayerGUID) & TerritoryPermission.Dig) != 0;

    public static bool CanPlace(Territory territory, PlayerData? player) => player == null ||
        AllowsOwner(territory, player) || territory.Settings.AllowPlacing ||
        (territory.PermissionsFor(player.PlayerGUID) & TerritoryPermission.Place) != 0;

    public static bool CanCross(Territory territory, PlayerData player, bool exiting) =>
        AllowsOwner(territory, player) ||
        (exiting ? territory.Settings.AllowExit : territory.Settings.AllowEntry) ||
        (territory.PermissionsFor(player.PlayerGUID) & (exiting ? TerritoryPermission.Exit : TerritoryPermission.Enter)) != 0;

    public static bool AllowsOwner(Territory territory, PlayerData? player) => player != null &&
        (player.PlayerGUID == territory.Owner || player.ServerManager || player.ServerMaster);

    private static Territory? Find(Project project, int x, int z) =>
        project.FindSubsystem<TerritorySubsystem>()?.Store.Find(x, z);

    private static bool Notify(ComponentMiner miner, string key, Territory? territory = null)
    {
        if (RunMode.Value is RunModeType.Gui && miner.ComponentPlayer?.PlayerData.IsMainPlayer == true)
        {
            var text = LanguageManager.Get("TerritoryProtection", key);
            if (territory != null)
            {
                var world = miner.Project.FindSubsystem<TerritorySubsystem>()!;
                var hint = key == "StoneOwnerOnly" ? "OwnedBy" :
                    territory.Grants.ContainsKey(miner.ComponentPlayer.PlayerData.PlayerGUID) ? "AuthorizedHint" : "RequestHint";
                text += " " + string.Format(LanguageManager.Get("TerritoryAccess", hint), world.OwnerName(territory.Owner));
            }
            miner.ComponentPlayer.ComponentGui.DisplaySmallMessage(text, Color.Yellow, false, true);
            return true;
        }

        return false;
    }

    private static ComponentPlayer? BodyPlayer(ComponentBody body) => body.Player ??
        body.ChildBodies.Select(child => child.Player).FirstOrDefault(player => player != null);

    private static void AddBoundaryCollisions(TerrainCollisionBoxesContext context, Action<ComponentBody, Territory, bool> onCollision)
    {
        var player = BodyPlayer(context.Body)?.PlayerData;
        var store = context.Body.Project.FindSubsystem<TerritorySubsystem>()?.Store;
        if (player == null || store == null)
        {
            return;
        }

        foreach (var territory in store.Territories)
        {
            var exiting = territory.Contains(context.Body.Position);
            if (CanCross(territory, player, exiting))
            {
                continue;
            }

            var movement = context.Bounds.Center() - context.Body.Position;
            foreach (var box in territory.GetBlockingBoundaryBoxes(
                         context.Bounds.Min.Y - 1, context.Bounds.Max.Y + 1, movement, exiting))
            {
                if (context.Bounds.Intersection(box))
                {
                    context.CollisionBoxes.Add(new ComponentBody.CollisionBox
                    {
                        Box = box,
                        BlockValue = Terrain.MakeBlockValue(CurrentModRuntime.Value!.RequireWorldCatalog()
                            .GetBlock(TerritoryBlock.Id).BlockIndex),
                        Collided = body => onCollision(body, territory, exiting)
                    });
                }
            }
        }
    }
}
