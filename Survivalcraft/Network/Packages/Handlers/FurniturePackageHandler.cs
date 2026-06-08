using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class FurniturePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        FurnitureSet? furnitureSet;
        FurnitureDesign? furniture;
        ValuesDictionary? valuesDictionary;
        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        if (From == null)
        {
            return;
        }

        var playerData = subsystemPlayers.PlayersData.Find(x => x.Client == From);
        if (playerData is not { ComponentPlayer: not null })
        {
            return;
        }

        var creativeWidget = new CreativeInventoryWidget(playerData.ComponentPlayer.Entity);
        var furnitureInventoryPanel = creativeWidget.FurnitureInventoryPanel;

        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>();
        var subsystemFurnitureBlockBehavior = project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        switch (PackageEventType)
        {
            case EventType.TryAddDesignChain:
                valuesDictionary = CommonLib.ReadVDict(AddXml);
                furniture = new FurnitureDesign(FurnitureIndex, subsystemTerrain, valuesDictionary);
                subsystemFurnitureBlockBehavior.TryAddDesignChain(furniture, StartValue == 1);
                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.AddToFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == AddXml);
                furniture = subsystemFurnitureBlockBehavior.FurnitureDesigns.FirstOrDefault(f =>
                    f?.Index == FurnitureIndex);
                if (furniture != null)
                {
                    subsystemFurnitureBlockBehavior.AddToFurnitureSet(furniture, furnitureSet!);
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.MoveFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == AddXml);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.MoveFurnitureSet(furnitureSet, FurnitureIndex);
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.RenameFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == AddXml);
                if (furnitureSet != null)
                {
                    furnitureSet.Name = AddXml;
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.DeleteFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == AddXml);
                if (furnitureSet != null)
                {
                    var num = subsystemFurnitureBlockBehavior.FurnitureSets.IndexOf(furnitureSet);
                    subsystemFurnitureBlockBehavior.DeleteFurnitureSet(furnitureSet);
                    subsystemFurnitureBlockBehavior.GarbageCollectDesigns();
                    if (furnitureInventoryPanel.ComponentFurnitureInventory.FurnitureSet.Name == AddXml)
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
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.NewFurnitureSet:
                furnitureInventoryPanel.NewFurnitueSetLogic(AddXml, FromName);
                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.Add:
                valuesDictionary = CommonLib.ReadVDict(AddXml);
                furniture = new FurnitureDesign(FurnitureIndex, subsystemTerrain, valuesDictionary);
                if (subsystemPlayers.MainPlayer != null)
                {
                    subsystemFurnitureBlockBehavior.CreateDesign(subsystemPlayers.MainPlayer.ComponentMiner, furniture,
                        _pointDict, CellFace, StartValue, false);
                }

                break;
            case EventType.RequestAdd:
                valuesDictionary = CommonLib.ReadVDict(AddXml);
                furniture = new FurnitureDesign(0, subsystemTerrain, valuesDictionary);
                subsystemPlayers.FindPlayerByClientId(From.ID, player =>
                {
                    furniture = subsystemFurnitureBlockBehavior.CreateDesign(player.ComponentMiner, furniture,
                        _pointDict,
                        CellFace, StartValue);
                    //回复添加家具包
                    netNode.QueuePackage(new FurniturePackage(furniture, _pointDict, CellFace, StartValue));
                });
                break;
            case EventType.RemoveFurnitureDesigns:
                for (var k = 0; k < subsystemFurnitureBlockBehavior.FurnitureDesigns.Length; k++)
                {
                    var obj = subsystemFurnitureBlockBehavior.FurnitureDesigns[k];
                    if (obj == null)
                    {
                        continue;
                    }

                    foreach (var item in ToRemoveList)
                    {
                        if (obj.Index == item)
                        {
                            obj.Index = -1;
                            subsystemFurnitureBlockBehavior.FurnitureDesigns[k] = null;
                            break;
                        }
                    }
                }

                break;
        }
    }
}

public sealed class FurniturePackageHandler : PackageHandlerBase<FurniturePackage>
{
    public override void Handle(FurniturePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(FurniturePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
