using EntitySystem.TemplatesDatabase;

namespace Game.Network.Packages.Handlers;

public sealed class FurniturePackageHandler : PackageHandlerBase<FurniturePackage>
{
    public override void Handle(FurniturePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(FurniturePackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        FurnitureSet? furnitureSet;
        FurnitureDesign? furniture;
        ValuesDictionary? valuesDictionary;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        if (package.From == null)
        {
            return;
        }

        var playerData = subsystemPlayers.PlayersData.Find(x => x.Client == package.From);
        if (playerData is not { ComponentPlayer: not null })
        {
            return;
        }

        var creativeWidget = new CreativeInventoryWidget(playerData.ComponentPlayer.Entity);
        var furnitureInventoryPanel = creativeWidget.FurnitureInventoryPanel;

        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>();
        var subsystemFurnitureBlockBehavior = project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        switch (package.PackageEventType)
        {
            case FurniturePackage.EventType.TryAddDesignChain:
                valuesDictionary = CommonLib.ReadVDict(package.AddXml);
                furniture = new FurnitureDesign(package.FurnitureIndex, subsystemTerrain, valuesDictionary);
                subsystemFurnitureBlockBehavior.TryAddDesignChain(furniture, package.StartValue == 1);
                if (isServer)
                {
                    package.Except = package.From;
                    netNode.QueuePackage(package);
                }

                break;
            case FurniturePackage.EventType.AddToFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                furniture = subsystemFurnitureBlockBehavior.FurnitureDesigns.FirstOrDefault(f =>
                    f?.Index == package.FurnitureIndex);
                if (furniture != null)
                {
                    subsystemFurnitureBlockBehavior.AddToFurnitureSet(furniture, furnitureSet!);
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    package.Except = package.From;
                    netNode.QueuePackage(package);
                }

                break;
            case FurniturePackage.EventType.MoveFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.MoveFurnitureSet(furnitureSet, package.FurnitureIndex);
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    package.Except = package.From;
                    netNode.QueuePackage(package);
                }

                break;
            case FurniturePackage.EventType.RenameFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                if (furnitureSet != null)
                {
                    furnitureSet.Name = package.AddXml;
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    package.Except = package.From;
                    netNode.QueuePackage(package);
                }

                break;
            case FurniturePackage.EventType.DeleteFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == package.AddXml);
                if (furnitureSet != null)
                {
                    var num = subsystemFurnitureBlockBehavior.FurnitureSets.IndexOf(furnitureSet);
                    subsystemFurnitureBlockBehavior.DeleteFurnitureSet(furnitureSet);
                    subsystemFurnitureBlockBehavior.GarbageCollectDesigns();
                    if (furnitureInventoryPanel.ComponentFurnitureInventory.FurnitureSet.Name == package.AddXml)
                    {
                        furnitureInventoryPanel.ComponentFurnitureInventory.FurnitureSet =
                            num > 0
                                ? subsystemFurnitureBlockBehavior.FurnitureSets[num - 1]
                                : FurnitureSetDefault.Default;
                    }

                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    package.Except = package.From;
                    netNode.QueuePackage(package);
                }

                break;
            case FurniturePackage.EventType.NewFurnitureSet:
                furnitureInventoryPanel.NewFurnitueSetLogic(package.AddXml, package.FromName);
                if (isServer)
                {
                    package.Except = package.From;
                    netNode.QueuePackage(package);
                }

                break;
            case FurniturePackage.EventType.Add:
                valuesDictionary = CommonLib.ReadVDict(package.AddXml);
                furniture = new FurnitureDesign(package.FurnitureIndex, subsystemTerrain, valuesDictionary);
                if (subsystemPlayers.MainPlayer != null)
                {
                    subsystemFurnitureBlockBehavior.CreateDesign(subsystemPlayers.MainPlayer.ComponentMiner, furniture,
                        package.PointDict, package.CellFace, package.StartValue, false);
                }

                break;
            case FurniturePackage.EventType.RequestAdd:
                valuesDictionary = CommonLib.ReadVDict(package.AddXml);
                furniture = new FurnitureDesign(0, subsystemTerrain, valuesDictionary);
                subsystemPlayers.FindPlayerByClientId(package.From.ID, player =>
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
                        )
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
    }
}
