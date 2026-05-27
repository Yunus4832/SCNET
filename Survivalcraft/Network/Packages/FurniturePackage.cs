using EntitySystem.TemplatesDatabase;

using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class FurniturePackage : IPackage
{
    public enum EventType
    {
        RequestAdd,
        Add,
        NewFurnitureSet,
        DeleteFurnitureSet,
        RenameFurnitureSet,
        MoveFurnitureSet,
        AddToFurnitureSet,
        TryAddDesignChain,
        RemoveFurnitureDesigns
    }

    private string _addXml = string.Empty;

    private CellFace _cellFace;

    private EventType _eventType;

    private string _fromName = string.Empty;

    private int _furnitureIndex;

    private readonly Dictionary<Point3, int> _pointDict = new();

    private int _startValue;

    private readonly List<int> _toRemoveList = [];

    public byte ID => (byte)PackageType.Furniture;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public FurniturePackage()
    {
    }

    public FurniturePackage(FurnitureSet furnitureSet)
    {
        _eventType = EventType.NewFurnitureSet;
        _addXml = furnitureSet.Name;
        _fromName = furnitureSet.ImportedFrom;
    }

    public FurniturePackage(List<int> list)
    {
        _eventType = EventType.RemoveFurnitureDesigns;
        _toRemoveList.AddRange(list);
    }


    public FurniturePackage(string designName)
    {
        _eventType = EventType.DeleteFurnitureSet;
        _addXml = designName;
    }

    public FurniturePackage(string oldName, string newName)
    {
        _eventType = EventType.RenameFurnitureSet;
        _addXml = oldName;
        _fromName = newName;
    }

    public FurniturePackage(FurnitureSet furnitureSet, int move)
    {
        _eventType = EventType.MoveFurnitureSet;
        _furnitureIndex = move;
        _addXml = furnitureSet.Name;
    }

    public FurniturePackage(FurnitureDesign design, FurnitureSet furnitureSet)
    {
        _eventType = EventType.AddToFurnitureSet;
        _furnitureIndex = design.Index;
        _addXml = furnitureSet.Name;
    }

    public FurniturePackage(FurnitureDesign design, bool garbageCollectIfNeeded)
    {
        _eventType = EventType.TryAddDesignChain;
        _furnitureIndex = design.Index;
        var dict = design.Save();
        _addXml = CommonLib.SerializeVDict(dict);
        _startValue = garbageCollectIfNeeded ? 1 : 0;
    }

    public FurniturePackage(FurnitureDesign design, Dictionary<Point3, int> list, CellFace cellFace, int value,
        bool isRequest = false)
    {
        _eventType = isRequest ? EventType.RequestAdd : EventType.Add;
        _furnitureIndex = design.Index;
        var dict = design.Save();
        _addXml = CommonLib.SerializeVDict(dict);
        foreach (var k in list)
        {
            _pointDict.Add(k.Key, k.Value);
        }

        _cellFace = cellFace;
        _startValue = value;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(_eventType);
        switch (_eventType)
        {
            case EventType.TryAddDesignChain:
                writer.Write(_addXml);
                writer.Write(_furnitureIndex);
                writer.Write(_startValue);
                break;
            case EventType.AddToFurnitureSet:
            case EventType.MoveFurnitureSet:
                writer.Write(_addXml);
                writer.Write(_furnitureIndex);
                break;
            case EventType.RenameFurnitureSet:
                writer.Write(_addXml);
                writer.Write(_fromName);
                break;
            case EventType.DeleteFurnitureSet:
                writer.Write(_addXml);
                break;
            case EventType.NewFurnitureSet:
                writer.Write(_addXml);
                writer.Write(!string.IsNullOrEmpty(_fromName));
                if (!string.IsNullOrEmpty(_fromName))
                {
                    writer.Write(_fromName);
                }

                break;
            case EventType.RemoveFurnitureDesigns:
                writer.Write(_toRemoveList.Count);
                foreach (var item in _toRemoveList)
                {
                    writer.Write(item);
                }

                break;
            case EventType.Add:
            case EventType.RequestAdd:
                writer.Write(_furnitureIndex);
                writer.Write(_addXml);
                writer.Write(_pointDict.Count);
                foreach (var k in _pointDict)
                {
                    writer.Write(k.Key);
                    writer.Write(k.Value);
                }

                writer.Write(_cellFace);
                writer.Write(_startValue);
                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _eventType = reader.ReadEnum<EventType>();
        switch (_eventType)
        {
            case EventType.TryAddDesignChain:
                _addXml = reader.ReadString();
                _furnitureIndex = reader.ReadInt32();
                _startValue = reader.ReadInt32();
                break;
            case EventType.AddToFurnitureSet:
            case EventType.MoveFurnitureSet:
                _addXml = reader.ReadString();
                _furnitureIndex = reader.ReadInt32();
                break;
            case EventType.RenameFurnitureSet:
                _addXml = reader.ReadString();
                _fromName = reader.ReadString();
                break;
            case EventType.DeleteFurnitureSet:
                _addXml = reader.ReadString();
                break;
            case EventType.NewFurnitureSet:
                _addXml = reader.ReadString();
                if (reader.ReadBoolean())
                {
                    _fromName = reader.ReadString();
                }

                break;
            case EventType.RemoveFurnitureDesigns:
                var c = reader.ReadInt32();
                for (var i = 0; i < c; i++)
                {
                    _toRemoveList.Add(reader.ReadInt32());
                }

                break;
            case EventType.Add:
            case EventType.RequestAdd:
                _furnitureIndex = reader.ReadInt32();
                _addXml = reader.ReadString();
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    _pointDict.Add(reader.ReadPoint3(), reader.ReadInt32());
                }

                _cellFace = reader.ReadCellFace();
                _startValue = reader.ReadInt32();
                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
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
        switch (_eventType)
        {
            case EventType.TryAddDesignChain:
                valuesDictionary = CommonLib.ReadVDict(_addXml);
                furniture = new FurnitureDesign(_furnitureIndex, subsystemTerrain, valuesDictionary);
                subsystemFurnitureBlockBehavior.TryAddDesignChain(furniture, _startValue == 1);
                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.AddToFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == _addXml);
                furniture = subsystemFurnitureBlockBehavior.FurnitureDesigns.FirstOrDefault(f =>
                    f?.Index == _furnitureIndex);
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
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == _addXml);
                if (furnitureSet != null)
                {
                    subsystemFurnitureBlockBehavior.MoveFurnitureSet(furnitureSet, _furnitureIndex);
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.RenameFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == _addXml);
                if (furnitureSet != null)
                {
                    furnitureSet.Name = _addXml;
                    furnitureInventoryPanel.Invalidate();
                }

                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.DeleteFurnitureSet:
                furnitureSet = subsystemFurnitureBlockBehavior.FurnitureSets.Find(f => f.Name == _addXml);
                if (furnitureSet != null)
                {
                    var num = subsystemFurnitureBlockBehavior.FurnitureSets.IndexOf(furnitureSet);
                    subsystemFurnitureBlockBehavior.DeleteFurnitureSet(furnitureSet);
                    subsystemFurnitureBlockBehavior.GarbageCollectDesigns();
                    if (furnitureInventoryPanel.ComponentFurnitureInventory.FurnitureSet.Name == _addXml)
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
                furnitureInventoryPanel.NewFurnitueSetLogic(_addXml, _fromName);
                if (isServer)
                {
                    Except = From;
                    netNode.QueuePackage(this);
                }

                break;
            case EventType.Add:
                valuesDictionary = CommonLib.ReadVDict(_addXml);
                furniture = new FurnitureDesign(_furnitureIndex, subsystemTerrain, valuesDictionary);
                if (subsystemPlayers.MainPlayer != null)
                {
                    subsystemFurnitureBlockBehavior.CreateDesign(subsystemPlayers.MainPlayer.ComponentMiner, furniture,
                        _pointDict, _cellFace, _startValue, false);
                }

                break;
            case EventType.RequestAdd:
                valuesDictionary = CommonLib.ReadVDict(_addXml);
                furniture = new FurnitureDesign(0, subsystemTerrain, valuesDictionary);
                subsystemPlayers.FindPlayerByClientId(From.ID, player =>
                {
                    furniture = subsystemFurnitureBlockBehavior.CreateDesign(player.ComponentMiner, furniture,
                        _pointDict,
                        _cellFace, _startValue);
                    //回复添加家具包
                    netNode.QueuePackage(new FurniturePackage(furniture, _pointDict, _cellFace, _startValue));
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

                    foreach (var item in _toRemoveList)
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
