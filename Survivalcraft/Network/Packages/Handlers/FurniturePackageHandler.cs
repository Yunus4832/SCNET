using EntitySystem.TemplatesDatabase;

using Game.Messaging;
using Game.Network.Enums;

namespace Game.Network.Packages.Handlers;

public sealed class FurniturePackageHandler : PackageHandlerBase<FurniturePackage>
{
    public static void Submit(FurniturePackage package)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            NetworkSender.SendToServer(package);
            return;
        }

        PackageDispatcher.Handle(package,
            new PackageReceiveContext(CommonLib.Net, CommonLib.WorkType == WorkType.Server, CommonLib.Net.Self));
    }

    internal static bool AcceptsDirection(FurniturePackage.EventType type, bool isServer)
    {
        return type switch
        {
            FurniturePackage.EventType.RequestAdd or FurniturePackage.EventType.ImportFurnitureSet => isServer,
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

        if (GameManager.Project is null || !AcceptsDirection(package.PackageEventType, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        FurnitureSet? furnitureSet;
        FurnitureDesign? furniture;
        ValuesDictionary? valuesDictionary;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        if (isServer && context.Sender == null)
        {
            return;
        }

        var playerData = subsystemPlayers.PlayersData.Find(x => ReferenceEquals(x.Client, context.Sender));
        if (isServer && playerData is not { ComponentPlayer: not null })
        {
            return;
        }

        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>();
        var subsystemFurnitureBlockBehavior = project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        switch (package.PackageEventType)
        {
            case FurniturePackage.EventType.ImportFurnitureSet:
                if (subsystemFurnitureBlockBehavior.FurnitureSets.Count >= 32)
                {
                    SendImportFeedback(context, "25", []);
                    return;
                }

                var imported = package.ReadDesignChain(subsystemTerrain);
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

                SendImportFeedback(context, "1", [importedRoots.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                if (duplicateCount > 0)
                {
                    SendImportFeedback(context, "2", [duplicateCount.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                }

                if (skippedCount > 0)
                {
                    SendImportFeedback(context, "3", [skippedCount.ToString(System.Globalization.CultureInfo.InvariantCulture), "65535"]);
                }

                if (importedRoots.Count == 0)
                {
                    return;
                }

                var importedSet = subsystemFurnitureBlockBehavior.NewFurnitureSet(package.FromName, string.Empty);
                NetworkSender.SendGlobal(new FurniturePackage(importedSet));
                foreach (var root in importedRoots)
                {
                    subsystemFurnitureBlockBehavior.AddToFurnitureSet(root, importedSet);
                    NetworkSender.SendGlobal(new FurniturePackage(root, importedSet));
                }

                break;
            case FurniturePackage.EventType.DesignChain:
                var chain = package.ReadDesignChain(subsystemTerrain);
                if (chain.Count == 0)
                {
                    return;
                }

                foreach (var node in chain)
                {
                    subsystemFurnitureBlockBehavior.InstallNetworkDesign(node);
                }

                break;
            case FurniturePackage.EventType.AddToFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                furniture = subsystemFurnitureBlockBehavior.FurnitureDesigns.FirstOrDefault(f =>
                    f?.Index == package.FurnitureIndex);
                if (furniture == null || furnitureSet == null)
                {
                    return;
                }

                subsystemFurnitureBlockBehavior.AddToFurnitureSet(furniture, furnitureSet);
                if (isServer)
                {
                    NetworkSender.SendGlobal(package);
                }

                break;
            case FurniturePackage.EventType.MoveFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.MoveFurnitureSet(furnitureSet, package.FurnitureIndex);
                }

                if (isServer)
                {
                    NetworkSender.SendGlobal(package);
                }

                break;
            case FurniturePackage.EventType.RenameFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.RenameFurnitureSet(furnitureSet, package.FromName);
                }

                if (isServer)
                {
                    NetworkSender.SendGlobal(new FurniturePackage(package.AddXml, furnitureSet?.Name ?? package.FromName));
                }

                break;
            case FurniturePackage.EventType.DeleteFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.DeleteFurnitureSet(furnitureSet);
                    if (CommonLib.WorkType != WorkType.Client)
                    {
                        subsystemFurnitureBlockBehavior.GarbageCollectDesigns();
                    }
                }

                if (isServer)
                {
                    NetworkSender.SendGlobal(package);
                }

                break;
            case FurniturePackage.EventType.NewFurnitureSet:
                if (isServer && subsystemFurnitureBlockBehavior.FurnitureSets.Count >= 32)
                {
                    return;
                }

                var createdSet = subsystemFurnitureBlockBehavior.NewFurnitureSet(package.AddXml, package.FromName);
                if (isServer)
                {
                    NetworkSender.SendGlobal(new FurniturePackage(createdSet));
                }

                break;
            case FurniturePackage.EventType.Add:
                valuesDictionary = CommonLib.ReadVDict(package.AddXml);
                furniture = new FurnitureDesign(package.FurnitureIndex, subsystemTerrain, valuesDictionary);
                subsystemFurnitureBlockBehavior.InstallNetworkDesign(furniture);

                break;
            case FurniturePackage.EventType.RequestAdd:
                var interest = project.FindSubsystem<SubsystemNetworkInterest>(true)!;
                if (package.PointDict.Count is 0 or > 4096 ||
                    !interest.IsPositionRelevant(context.Sender!,
                        new Vector2(package.CellFace.X, package.CellFace.Z)) ||
                    package.PointDict.Any(point =>
                        !interest.IsPositionRelevant(context.Sender!, new Vector2(point.Key.X, point.Key.Z)) ||
                        subsystemTerrain!.Terrain.GetCellValue(point.Key.X, point.Key.Y, point.Key.Z) != point.Value))
                {
                    return;
                }

                valuesDictionary = CommonLib.ReadVDict(package.AddXml);
                furniture = new FurnitureDesign(0, subsystemTerrain, valuesDictionary);
                subsystemPlayers.FindPlayerByClientId(context.Sender!.ID, player =>
                {
                    furniture = subsystemFurnitureBlockBehavior.CreateDesign(player.ComponentMiner, furniture,
                        package.PointDict,
                        package.CellFace, package.StartValue);
                    // 回复添加家具包
                    netNode.QueuePackage(new FurniturePackage(
                            furniture,
                            package.PointDict,
                            package.CellFace,
                            package.StartValue
                        ), PackageAudience.Global
                    );
                });
                break;
            case FurniturePackage.EventType.RemoveFurnitureDesigns:
                for (var k = 0; k < subsystemFurnitureBlockBehavior.FurnitureDesigns.Length; k++)
                {
                    var obj = subsystemFurnitureBlockBehavior.FurnitureDesigns[k];
                    if (obj == null)
                    {
                        continue;
                    }

                    if (package.ToRemoveList.All(item => obj.Index != item))
                    {
                        continue;
                    }

                    obj.Index = -1;
                    subsystemFurnitureBlockBehavior.FurnitureDesigns[k] = null;
                }

                break;
        }

        subsystemFurnitureBlockBehavior.NotifyNetworkChange();
    }

    private static void SendImportFeedback(PackageReceiveContext context, string key, string[] arguments)
    {
        var message = GameMessage.LocalizedSystem(nameof(FurnitureInventoryPanel), key, arguments,
            presentation: GameMessagePresentation.Default);
        if (context.Sender == context.Node!.Self)
        {
            GameManager.Project!.FindSubsystem<SubsystemGameWidgets>(true)!.Messages.DisplayLocal(message);
        }
        else
        {
            NetworkSender.SendTo(context.Sender!, new MessagePackage(message));
        }
    }
}
