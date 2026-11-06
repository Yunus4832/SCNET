using Engine.Core.Mathematics;
using Engine.Core.Runtime;

using EntitySystem.Core;

using Game.Characters;
using Game.Commands;
using Game.Components;
using Game.Modding;
using Game.Protocol;
using Game.Protocol.Identity;
using Game.Terrains;
using Game.World;

namespace TerritoryStoneMod;

public static class TerritoryProtection
{
    public static readonly ResourceId AdminPermission = new(new ModId(ModEntry.ModId), "admin");

    public static PlayerAddress? ActorAddress(CharacterData? player) => player?.ControllerConnection?.PlayerAddress;

    public static void Register(IModContext context)
    {
        context.Commands.Permissions.Register(
            AdminPermission,
            new CommandPermissionDefinition(
                CommandDomain.World,
                description: new Game.Protocol.Localization.LocalizedText(
                    "Commands",
                    "Territory_AdminDescription",
                    "绕过领地保护并拆除领地石"
                )
            )
        );
        var states = context.Worlds.Register(
            new ResourceId(context.Manifest.ModId, "protection"),
            static _ => new ProtectionState()
        );
        context.Gameplay.OnMinerDigging(digging =>
            {
                var state = states.Get(WorldRuntimeContext.For(digging.Miner.Project));
                var point = digging.RaycastResult.CellFace.Point;
                var territory = Find(digging.Miner.Project, point.X, point.Z);
                if (territory != null)
                {
                    var player = digging.Miner.ComponentCharacter?.CharacterData;
                    var ownerOnly = Terrain.ExtractContents(digging.CellValue) ==
                                    Game.Modding.Blocks.WorldContentBinding.For(digging.Miner.Project).Catalog
                                        .GetBlock(TerritoryBlock.Id)
                                        .BlockIndex &&
                                    !AllowsOwner(territory, player);
                    if (!CanDig(territory, player) || ownerOnly)
                    {
                        digging.Cancel = true;
                        if (Time.FrameStartTime - state.LastDigDenialTime >= 2 &&
                            Notify(digging.Miner, ownerOnly ? "StoneOwnerOnly" : "DiggingRestricted", territory))
                        {
                            state.LastDigDenialTime = Time.FrameStartTime;
                        }
                    }
                }
            }
        );
        context.BlockBehaviors.OnBlockPlaced(placing =>
            {
                var subsystem = placing.Miner.Project.FindSubsystem<TerritorySubsystem>();
                if (subsystem == null)
                {
                    return;
                }

                var territory = subsystem.Store.Find(placing.X, placing.Z);
                if (territory != null && !CanPlace(territory, placing.Miner.ComponentCharacter?.CharacterData))
                {
                    placing.Cancel = true;
                    Notify(placing.Miner, "PlacingRestricted", territory);
                    return;
                }

                if (Terrain.ExtractContents(placing.PlacementData.Value) !=
                    Game.Modding.Blocks.WorldContentBinding.For(placing.Miner.Project).Catalog
                        .GetBlock(TerritoryBlock.Id).BlockIndex)
                {
                    return;
                }

                var owner = ActorAddress(placing.Miner.ComponentCharacter?.CharacterData);
                var point = new Point3(placing.X, placing.Y, placing.Z);
                if (owner is not { } subject || !subsystem.Store.CanCreate(subject, point))
                {
                    placing.Cancel = true;
                    Notify(placing.Miner, "ClaimUnavailable");
                }
            }
        );
        context.BlockBehaviors.OnBlockAdded(added =>
            {
                if (!WorldRuntimeContext.For(added.Terrain.Project).IsAuthority ||
                    Terrain.ExtractContents(added.Value) !=
                    Game.Modding.Blocks.WorldContentBinding.For(added.Terrain.Project).Catalog
                        .GetBlock(TerritoryBlock.Id)
                        .BlockIndex)
                {
                    return;
                }

                var owner = ActorAddress(added.Miner?.ComponentCharacter?.CharacterData);
                var subsystem = added.Terrain.Project.FindSubsystem<TerritorySubsystem>();
                if (subsystem != null &&
                    owner is { } subject &&
                    subsystem.Store.TryCreate(subject, new Point3(added.X, added.Y, added.Z)))
                {
                    subsystem.Store.TryGet(subject, out var territory);
                    subsystem.PublishUpdate(territory!);
                }
            }
        );
        context.BlockBehaviors.OnBlockRemoved(removed =>
            {
                if (WorldRuntimeContext.For(removed.Terrain.Project).IsAuthority &&
                    Terrain.ExtractContents(removed.Value) ==
                    Game.Modding.Blocks.WorldContentBinding.For(removed.Terrain.Project).Catalog
                        .GetBlock(TerritoryBlock.Id)
                        .BlockIndex)
                {
                    var subsystem = removed.Terrain.Project.FindSubsystem<TerritorySubsystem>();
                    var territory = subsystem?.Store.Find(removed.X, removed.Z);
                    if (territory != null && subsystem!.Store.RemoveStone(new Point3(removed.X, removed.Y, removed.Z)))
                    {
                        subsystem.PublishRemoval(territory.Owner);
                    }
                }
            }
        );
        context.Gameplay.OnTerrainCellChanging(changing =>
            {
                var player = changing.Miner?.ComponentCharacter?.CharacterData;
                var territory = Find(changing.Terrain.Project, changing.X, changing.Z);
                if (player == null || territory == null)
                {
                    return;
                }

                var oldContents = Terrain.ExtractContents(changing.OldValue);
                var newContents = Terrain.ExtractContents(changing.NewValue);
                changing.Cancel |= oldContents != 0 && newContents == 0 && !CanDig(territory, player) ||
                                   oldContents == 0 && newContents != 0 && !CanPlace(territory, player);
                changing.Cancel |= oldContents ==
                                   Game.Modding.Blocks.WorldContentBinding.For(changing.Terrain.Project).Catalog
                                       .GetBlock(TerritoryBlock.Id).BlockIndex &&
                                   !AllowsOwner(territory, player);
            }
        );

        void OnBoundaryCollision(ComponentBody body, Territory territory, bool exiting)
        {
            var runtime = WorldRuntimeContext.For(body.Project);
            var state = states.Get(runtime);
            if (Time.FrameStartTime - state.LastBoundaryDenialTime >= 2 &&
                BodyPlayer(body)?.CharacterData.IsLocalCharacter == true &&
                runtime.HasPresentation)
            {
                Notify(
                    BodyPlayer(body)!.ComponentMiner,
                    exiting ? "ExitRestricted" : "EntryRestricted",
                    territory
                );
                state.LastBoundaryDenialTime = Time.FrameStartTime;
            }
        }

        context.Gameplay.OnTerrainCollisionBoxes(collision => AddBoundaryCollisions(collision, OnBoundaryCollision));
    }

    public static bool CanDig(Territory territory, CharacterData? player) => player == null ||
                                                                             AllowsOwner(territory, player) ||
                                                                             territory.Settings.AllowDigging ||
                                                                             HasGrant(
                                                                                 territory,
                                                                                 player,
                                                                                 TerritoryPermission.Dig
                                                                             );

    public static bool CanPlace(Territory territory, CharacterData? player) => player == null ||
                                                                               AllowsOwner(territory, player) ||
                                                                               territory.Settings.AllowPlacing ||
                                                                               HasGrant(
                                                                                   territory,
                                                                                   player,
                                                                                   TerritoryPermission.Place
                                                                               );

    public static bool CanCross(Territory territory, CharacterData player, bool exiting) =>
        AllowsOwner(territory, player) ||
        (exiting ? territory.Settings.AllowExit : territory.Settings.AllowEntry) ||
        HasGrant(territory, player, exiting ? TerritoryPermission.Exit : TerritoryPermission.Enter);

    private static bool HasGrant(Territory territory, CharacterData player, TerritoryPermission permission)
    {
        return ActorAddress(player) is { } address && (territory.PermissionsFor(address) & permission) != 0;
    }

    public static bool AllowsOwner(Territory territory, CharacterData? player)
    {
        if (ActorAddress(player) is not { } address) { return false; }

        return address == territory.Owner ||
               player!.ControllerConnection is { } connection &&
               Game.Players.WorldConnectionResolver.FindPlayer(connection) is { Ban: null } subject &&
               subject.Permissions.Grants.Any(grant => grant.Permission == AdminPermission);
    }

    private static Territory? Find(Project project, int x, int z) =>
        project.FindSubsystem<TerritorySubsystem>()?.Store.Find(x, z);

    private static bool Notify(ComponentMiner miner, string key, Territory? territory = null)
    {
        return miner.ComponentCharacter is { } character &&
               miner.Project.FindSubsystem<TerritorySubsystem>()?.RequestProtectionNotice(character, key, territory) ==
               true;
    }

    private static ComponentCharacter? BodyPlayer(ComponentBody body) => body.Character ??
                                                                         body.ChildBodies.Select(child =>
                                                                                 child.Character
                                                                             )
                                                                             .FirstOrDefault(player => player != null);

    private static void AddBoundaryCollisions(
        TerrainCollisionBoxesContext context,
        Action<ComponentBody, Territory, bool> onCollision
    )
    {
        var player = BodyPlayer(context.Body)?.CharacterData;
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
                         context.Bounds.Min.Y - 1,
                         context.Bounds.Max.Y + 1,
                         movement,
                         exiting
                     ))
            {
                if (context.Bounds.Intersection(box))
                {
                    context.CollisionBoxes.Add(
                        new ComponentBody.CollisionBox
                        {
                            Box = box,
                            BlockValue = Terrain.MakeBlockValue(
                                Game.Modding.Blocks.WorldContentBinding.For(context.Body.Project).Catalog
                                    .GetBlock(TerritoryBlock.Id).BlockIndex
                            ),
                            Collided = body => onCollision(body, territory, exiting)
                        }
                    );
                }
            }
        }
    }

    private sealed class ProtectionState : IDisposable
    {
        public double LastDigDenialTime = double.NegativeInfinity;

        public double LastBoundaryDenialTime = double.NegativeInfinity;

        public void Dispose()
        {
        }
    }
}
