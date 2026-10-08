using Engine.Core;

using EntitySystem.Core;

using Game;
using Game.Components;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;
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
                var ownerOnly = Terrain.ExtractContents(digging.CellValue) == TerritoryBlock.Index &&
                                !AllowsOwner(territory, player);
                if (!Allows(territory, player, true) || ownerOnly)
                {
                    digging.Cancel = true;
                    if (Time.FrameStartTime - lastDigDenialTime >= 2 &&
                        Notify(digging.Miner, ownerOnly ? "StoneOwnerOnly" : "Protected"))
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
            if (territory != null && !Allows(territory, placing.Miner.ComponentPlayer?.PlayerData, true))
            {
                placing.Cancel = true;
                Notify(placing.Miner, "Protected");
                return;
            }

            if (Terrain.ExtractContents(placing.PlacementData.Value) != TerritoryBlock.Index)
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

            if (placing.Miner.Project.FindSubsystem<SubsystemTerrain>(true)!.Terrain
                    .GetCellContents(placing.X, placing.Y, placing.Z) is 233 or 232 or 229 or 226)
            {
                placing.Cancel = true;
                Notify(placing.Miner, "PlacementBlocked");
            }
        });
        context.BlockBehaviors.OnBlockAdded(added =>
        {
            if (CommonLib.WorkType == WorkType.Client || Terrain.ExtractContents(added.Value) != TerritoryBlock.Index)
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
            if (CommonLib.WorkType != WorkType.Client && Terrain.ExtractContents(removed.Value) == TerritoryBlock.Index)
            {
                var subsystem = removed.Terrain.Project.FindSubsystem<TerritorySubsystem>();
                var territory = subsystem?.Store.Find(removed.X, removed.Z);
                if (territory != null && subsystem!.Store.RemoveStone(new Point3(removed.X, removed.Y, removed.Z)))
                {
                    subsystem.PublishRemoval(territory.Owner);
                }
            }
        });
        context.BlockBehaviors.OnInteract(interacting =>
        {
            var point = interacting.RaycastResult.CellFace.Point;
            var territory = Find(interacting.Miner.Project, point.X, point.Z);
            interacting.Cancel |= territory != null && !Allows(territory, interacting.Miner.ComponentPlayer?.PlayerData, true);
        });
        context.BlockBehaviors.OnUse(usingBlock =>
        {
            if (usingBlock.Miner.Raycast(usingBlock.Ray, RaycastMode.Digging) is TerrainRaycastResult hit)
            {
                var territory = Find(usingBlock.Miner.Project, hit.CellFace.X, hit.CellFace.Z);
                usingBlock.Cancel |= territory != null && !Allows(territory, usingBlock.Miner.ComponentPlayer?.PlayerData, true);
            }
        });
        context.Gameplay.OnTerrainCellChanging(changing =>
        {
            var territory = Find(changing.Terrain.Project, changing.X, changing.Z);
            if (territory == null)
            {
                return;
            }

            var player = changing.Miner?.ComponentPlayer?.PlayerData;
            changing.Cancel |= changing.Miner != null && !Allows(territory, player, true) ||
                territory.RestrictEntry && territory.IsBorder(changing.X, changing.Z) && !Allows(territory, player, false);
            changing.Cancel |= changing.Miner != null && Terrain.ExtractContents(changing.OldValue) == TerritoryBlock.Index &&
                !AllowsOwner(territory, player);
        });
        context.Gameplay.OnCellIgniting(igniting =>
        {
            var territory = Find(igniting.Terrain.Project, igniting.Point.X, igniting.Point.Z);
            igniting.Cancel |= territory != null && !Allows(territory, igniting.Miner?.ComponentPlayer?.PlayerData, false);
        });
        context.Gameplay.OnExplosionPointProcessing(exploding =>
        {
            var territory = Find(exploding.Explosions.Project, exploding.Point.X, exploding.Point.Z);
            exploding.Cancel |= territory != null && !Allows(territory, exploding.Player, false);
        });
        context.Gameplay.OnMounting(mounting =>
        {
            var position = mounting.Mount.ComponentBody.Position;
            var territory = Find(mounting.Rider.Project, Terrain.ToCell(position.X), Terrain.ToCell(position.Z));
            mounting.Cancel |= territory != null && !Allows(territory,
                mounting.Rider.Entity.FindComponent<ComponentPlayer>()?.PlayerData, true);
        });
        context.Gameplay.OnPistonBlockMoving(moving =>
        {
            var territory = Find(moving.Pistons.Project, moving.Point.X, moving.Point.Z);
            moving.Cancel |= territory is { RestrictEntry: true } && territory.IsBorder(moving.Point.X, moving.Point.Z);
        });
        context.Gameplay.OnMovingBlockSetTerrainCollision(moving =>
        {
            var store = moving.MovingBlocks.Project.FindSubsystem<TerritorySubsystem>()?.Store;
            if (store == null)
            {
                return;
            }

            for (var x = moving.Min.X; x < moving.Max.X; x++)
            {
                for (var z = moving.Min.Z; z < moving.Max.Z; z++)
                {
                    if (store.Find(x, z) is { RestrictEntry: true } territory && territory.IsBorder(x, z))
                    {
                        moving.BlockSet.Stop();
                        return;
                    }
                }
            }
        });
        var lastBoundaryDenialTime = double.NegativeInfinity;
        void OnBoundaryCollision(ComponentBody body)
        {
            if (Time.FrameStartTime - lastBoundaryDenialTime >= 2 &&
                body.Player?.PlayerData.IsMainPlayer == true && RunMode.Value is RunModeType.Gui)
            {
                body.Player.ComponentGui.DisplaySmallMessage(
                    LanguageManager.Get("TerritoryProtection", "EntryRestricted"), Color.Yellow, false, true);
                lastBoundaryDenialTime = Time.FrameStartTime;
            }
        }

        context.Gameplay.OnTerrainCollisionBoxes(collision => AddBoundaryCollisions(collision, OnBoundaryCollision));
    }

    public static bool Allows(Territory territory, PlayerData? player, bool allowAnonymous) =>
        player == null ? allowAnonymous : AllowsOwner(territory, player) ||
            territory.ApplyToTeam && player.IsInGroup(territory.Owner);

    public static bool AllowsOwner(Territory territory, PlayerData? player) => player != null &&
        (player.PlayerGUID == territory.Owner || player.ServerManager || player.ServerMaster);

    private static Territory? Find(Project project, int x, int z) =>
        project.FindSubsystem<TerritorySubsystem>()?.Store.Find(x, z);

    private static bool Notify(ComponentMiner miner, string key)
    {
        if (RunMode.Value is RunModeType.Gui && miner.ComponentPlayer?.PlayerData.IsMainPlayer == true)
        {
            miner.ComponentPlayer.ComponentGui.DisplaySmallMessage(
                LanguageManager.Get("TerritoryProtection", key), Color.Yellow, false, true);
            return true;
        }

        return false;
    }

    private static void AddBoundaryCollisions(TerrainCollisionBoxesContext context, Action<ComponentBody> onCollision)
    {
        var player = context.Body.Player?.PlayerData;
        var store = context.Body.Project.FindSubsystem<TerritorySubsystem>()?.Store;
        if (player == null || store == null)
        {
            return;
        }

        foreach (var territory in store.Territories)
        {
            if (!territory.RestrictEntry || Allows(territory, player, true) ||
                territory.Contains(context.Body.Position))
            {
                continue;
            }

            foreach (var box in territory.GetBoundaryBoxes(context.Bounds.Min.Y - 1, context.Bounds.Max.Y + 1))
            {
                if (context.Bounds.Intersection(box))
                {
                    context.CollisionBoxes.Add(new ComponentBody.CollisionBox
                    {
                        Box = box,
                        BlockValue = 46,
                        Collided = onCollision
                    });
                }
            }
        }
    }
}
