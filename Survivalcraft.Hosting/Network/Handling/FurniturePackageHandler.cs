using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Messaging;

namespace Game.Network.Handling;

public sealed class FurniturePackageHandler : PackageHandlerBase<FurniturePackage>
{
    public static void Submit(Project project, FurniturePackage package)
    {
        NetworkSender.SendToServer(WorldRuntimeContext.For(project), package);
    }

    internal static bool AcceptsDirection(FurniturePackage.EventType type, bool isServer)
    {
        return type switch
        {
            FurniturePackage.EventType.RequestAdd or FurniturePackage.EventType.ImportFurnitureSet
                or FurniturePackage.EventType.RenameDesignRequest => isServer,
            FurniturePackage.EventType.Add or FurniturePackage.EventType.RemoveFurnitureDesigns or
                FurniturePackage.EventType.DesignChain => !isServer,
            FurniturePackage.EventType.NewFurnitureSet or FurniturePackage.EventType.DeleteFurnitureSet or
                FurniturePackage.EventType.RenameFurnitureSet or FurniturePackage.EventType.MoveFurnitureSet or
                FurniturePackage.EventType.AddToFurnitureSet => true,
            _ => false
        };
    }

    public override void Handle(FurniturePackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(FurniturePackage)}");
            return;
        }

        if (context.Project is null || !AcceptsDirection(package.PackageEventType, isServer))
        {
            return;
        }

        var project = context.Project;
        FurnitureSet? furnitureSet;
        FurnitureDesign? furniture;
        ValuesDictionary? valuesDictionary;
        var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
        if (isServer && context.Sender == null)
        {
            return;
        }

        var characterData =
            subsystemCharacters.Characters.Find(x => ReferenceEquals(x.ControllerConnection, context.Sender));
        if (isServer && characterData is not { ComponentCharacter: not null })
        {
            return;
        }

        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>();
        var subsystemFurnitureBlockBehavior = project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        switch (package)
        {
            case FurnitureDesignRenameRequestPackage rename:
            {
                if (isServer) { ApplyDesignRename(project, context.Sender!, rename); }

                return;
            }
            case FurnitureSetImportPackage message1:
            {
                if (subsystemFurnitureBlockBehavior.FurnitureSets.Count >= 32)
                {
                    SendImportFeedback(context, "25", []);
                    return;
                }

                var imported = FurnitureSnapshotSerializer.ReadDesignChain(message1.DesignXml, subsystemTerrain);
                var importedRoots = new List<FurnitureDesign>();
                var duplicateCount = 0;
                var skippedCount = 0;
                subsystemFurnitureBlockBehavior.GarbageCollectDesigns();
                foreach (var importedChain in FurnitureDesign.ListChains(imported))
                {
                    var root = subsystemFurnitureBlockBehavior.TryAddDesignChain(importedChain[0], false);
                    if (root == importedChain[0])
                    {
                        importedRoots.Add(root);
                    }
                    else if (root == null)
                    {
                        skippedCount++;
                    }
                    else
                    {
                        duplicateCount++;
                    }
                }

                SendImportFeedback(
                    context,
                    "1",
                    [importedRoots.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)]
                );
                if (duplicateCount > 0)
                {
                    SendImportFeedback(
                        context,
                        "2",
                        [duplicateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)]
                    );
                }

                if (skippedCount > 0)
                {
                    SendImportFeedback(
                        context,
                        "3",
                        [skippedCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "65535"]
                    );
                }

                if (importedRoots.Count == 0)
                {
                    return;
                }

                var importedSet = subsystemFurnitureBlockBehavior.NewFurnitureSet(message1.Name, string.Empty);
                NetworkSender.SendGlobal(project, FurniturePackageBuilder.Capture(importedSet));
                foreach (var root in importedRoots)
                {
                    subsystemFurnitureBlockBehavior.AddToFurnitureSet(root, importedSet);
                    NetworkSender.SendGlobal(project, FurniturePackageBuilder.Capture(root, importedSet));
                }

                break;
            }
            case FurnitureDesignChainPackage message2:
            {
                var chain = FurnitureSnapshotSerializer.ReadDesignChain(message2.DesignXml, subsystemTerrain);
                if (chain.Count == 0)
                {
                    return;
                }

                foreach (var node in chain)
                {
                    subsystemFurnitureBlockBehavior.InstallNetworkDesign(node);
                }

                break;
            }
            case FurnitureSetAddDesignPackage message3:
            {
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == message3.SetName);
                furniture = subsystemFurnitureBlockBehavior.FurnitureDesigns.FirstOrDefault(f =>
                    f?.Index == message3.FurnitureIndex
                );
                if (furniture == null || furnitureSet == null)
                {
                    return;
                }

                subsystemFurnitureBlockBehavior.AddToFurnitureSet(furniture, furnitureSet);
                if (isServer)
                {
                    NetworkSender.SendGlobal(project, message3);
                }

                break;
            }
            case FurnitureSetMovePackage message4:
            {
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == message4.SetName);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.MoveFurnitureSet(furnitureSet, message4.FurnitureIndex);
                }

                if (isServer)
                {
                    NetworkSender.SendGlobal(project, message4);
                }

                break;
            }
            case FurnitureSetRenamedPackage message5:
            {
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == message5.OldName);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.RenameFurnitureSet(furnitureSet, message5.NewName);
                }

                if (isServer)
                {
                    NetworkSender.SendGlobal(
                        project,
                        new FurnitureSetRenamedPackage
                            { OldName = message5.OldName, NewName = furnitureSet?.Name ?? message5.NewName }
                    );
                }

                break;
            }
            case FurnitureSetDeletedPackage message6:
            {
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == message6.Name);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.DeleteFurnitureSet(furnitureSet);
                    if (isServer)
                    {
                        subsystemFurnitureBlockBehavior.GarbageCollectDesigns();
                    }
                }

                if (isServer)
                {
                    NetworkSender.SendGlobal(project, message6);
                }

                break;
            }
            case FurnitureSetCreatedPackage message7:
            {
                if (isServer && subsystemFurnitureBlockBehavior.FurnitureSets.Count >= 32)
                {
                    return;
                }

                var createdSet = subsystemFurnitureBlockBehavior.NewFurnitureSet(message7.Name, message7.ImportedFrom);
                if (isServer)
                {
                    NetworkSender.SendGlobal(project, FurniturePackageBuilder.Capture(createdSet));
                }

                break;
            }
            case FurnitureDesignAddedPackage message8:
            {
                valuesDictionary = ValuesDictionaryXmlCodec.Deserialize(message8.DesignXml);
                furniture = new FurnitureDesign(
                    message8.FurnitureIndex,
                    WorldContentBinding.For(context.Runtime).Catalog,
                    subsystemTerrain,
                    valuesDictionary
                );
                subsystemFurnitureBlockBehavior.InstallNetworkDesign(furniture);

                break;
            }
            case FurnitureDesignAddRequestPackage message9:
            {
                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                if (message9.PointDict.Count is 0 or > 4096 ||
                    !interest.IsPositionRelevant(
                        context.Sender!,
                        new Vector2(message9.CellFace.X, message9.CellFace.Z)
                    ) ||
                    message9.PointDict.Any(point =>
                        !interest.IsPositionRelevant(context.Sender!, new Vector2(point.Key.X, point.Key.Z)) ||
                        subsystemTerrain!.Terrain.GetCellValue(point.Key.X, point.Key.Y, point.Key.Z) != point.Value
                    ))
                {
                    return;
                }

                valuesDictionary = ValuesDictionaryXmlCodec.Deserialize(message9.DesignXml);
                furniture = new FurnitureDesign(
                    0,
                    WorldContentBinding.For(context.Runtime).Catalog,
                    subsystemTerrain,
                    valuesDictionary
                );
                subsystemCharacters.FindCharacterByClientId(
                    context.Sender!.ID,
                    player =>
                    {
                        furniture = subsystemFurnitureBlockBehavior.CreateDesign(
                            player.ComponentMiner,
                            furniture,
                            message9.PointDict,
                            message9.CellFace,
                            message9.StartValue
                        );
                        // 回复添加家具包
                        netNode.QueuePackage(
                            FurniturePackageBuilder.Capture(
                                furniture,
                                message9.PointDict,
                                message9.CellFace,
                                message9.StartValue
                            ),
                            PackageAudience.Global
                        );
                    }
                );
                break;
            }
            case FurnitureDesignsRemovedPackage message10:
            {
                for (var k = 0; k < subsystemFurnitureBlockBehavior.FurnitureDesigns.Length; k++)
                {
                    var obj = subsystemFurnitureBlockBehavior.FurnitureDesigns[k];
                    if (obj == null)
                    {
                        continue;
                    }

                    if (message10.ToRemoveList.All(item => obj.Index != item))
                    {
                        continue;
                    }

                    obj.Index = -1;
                    subsystemFurnitureBlockBehavior.FurnitureDesigns[k] = null;
                }

                break;
            }
        }

        subsystemFurnitureBlockBehavior.NotifyNetworkChange();
    }

    internal static bool ApplyDesignRename(
        Project project,
        WorldConnection sender,
        FurnitureDesignRenameRequestPackage request
    )
    {
        if (!WorldRuntimeContext.IsProjectActive(project) ||
            !ReferenceEquals(sender.World, WorldRuntimeContext.For(project)) ||
            request.Source.Y is < 0 or >= 256 ||
            request.Source.Face is < 0 or >= 6)
        {
            return false;
        }

        var terrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        var furniture = project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        if (!project.FindSubsystem<SubsystemNetworkInterest>(true)!.IsPositionRelevant(
                sender,
                new Vector2(request.Source.X, request.Source.Z)
            ))
        {
            return false;
        }

        var value = terrain.Terrain.GetCellValue(request.Source.X, request.Source.Y, request.Source.Z);
        if (Terrain.ExtractContents(value) != BuiltInBlockRegistry.GetIndex<FurnitureBlock>() ||
            FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value)) != request.FurnitureIndex ||
            !furniture.TryRenameDesign(request.FurnitureIndex, request.Name))
        {
            return false;
        }

        NetworkSender.SendGlobal(
            project,
            FurniturePackageBuilder.Capture(furniture.GetDesign(request.FurnitureIndex)!)
        );
        return true;
    }

    private static void SendImportFeedback(PackageReceiveContext context, string key, string[] arguments)
    {
        var message = GameMessageLocalization.System(
            "FurnitureInventoryPanel",
            key,
            arguments,
            presentation: GameMessagePresentation.Default
        );
        NetworkSender.SendTo(context.Sender!, new MessagePackage(message));
    }
}
