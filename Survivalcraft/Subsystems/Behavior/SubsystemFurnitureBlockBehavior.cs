using System.Globalization;

using Engine.Serialization;

using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemFurnitureBlockBehavior : SubsystemBlockBehavior
{
    public const int MaxFurnitureSetNameLength = 64;

    private const string _typeName = "SubsystemFurnitureBlockBehavior";

    public readonly FurnitureDesign?[] FurnitureDesigns = new FurnitureDesign[ComponentFurnitureInventory.MaxDesign];

    private readonly List<FurnitureSet> _furnitureSets = [];

    private readonly Dictionary<Point3, List<FireParticleSystem>> _particleSystemsByCell = new();

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemItemsScanner _subsystemItemsScanner = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    public override int[] HandledBlocks => [];

    public ReadOnlyList<FurnitureSet> FurnitureSets => new(_furnitureSets);

    public FurnitureDesign? GetDesign(int index)
    {
        if (index < 0 || index >= FurnitureDesigns.Length)
        {
            return null;
        }

        return FurnitureDesigns[index];
    }

    private FurnitureDesign? FindMatchingDesign(FurnitureDesign design)
    {
        return FurnitureDesigns.OfType<FurnitureDesign>()
            .FirstOrDefault(furnitureDesign => furnitureDesign.Compare(design));
    }

    private FurnitureDesign? FindMatchingDesignChain(FurnitureDesign design)
    {
        var furnitureDesign = FindMatchingDesign(design);
        if (furnitureDesign != null && design.CompareChain(furnitureDesign))
        {
            return furnitureDesign;
        }

        return null;
    }

    private FurnitureDesign TryAddDesign(FurnitureDesign design)
    {
        //寻找已经存在的家具
        foreach (var furnitureDesign in FurnitureDesigns)
        {
            if (furnitureDesign != null && furnitureDesign.Compare(design))
            {
                return furnitureDesign;
            }
        }

        //将家具插入到为null的位置
        for (var j = 0; j < FurnitureDesigns.Length; j++)
        {
            if (FurnitureDesigns[j] is null)
            {
                AddDesign(j, design);
                return design;
            }
        }

        GarbageCollectDesigns();
        for (var k = 0; k < FurnitureDesigns.Length; k++)
        {
            if (FurnitureDesigns[k] == null)
            {
                AddDesign(k, design);
                return design;
            }
        }

        return design;
    }

    public FurnitureDesign? TryAddDesignChain(FurnitureDesign design, bool garbageCollectIfNeeded)
    {
        var furnitureDesign = FindMatchingDesignChain(design);
        if (furnitureDesign != null)
        {
            return furnitureDesign;
        }

        var list = design.ListChain();
        if (garbageCollectIfNeeded && FurnitureDesigns.Count(d => d == null) < list.Count)
        {
            GarbageCollectDesigns();
        }

        if (FurnitureDesigns.Count(d => d == null) < list.Count)
        {
            return null;
        }

        var num = 0;
        for (var i = 0; i < FurnitureDesigns.Length; i++)
        {
            if (num >= list.Count)
            {
                break;
            }

            if (FurnitureDesigns[i] == null)
            {
                AddDesign(i, list[num]);
                num++;
            }
        }

        if (num != list.Count)
        {
            throw new InvalidOperationException("public error.");
        }

        return design;
    }

    public void ScanDesign(CellFace start, Vector3 direction, ComponentMiner componentMiner)
    {
        FurnitureDesign? design;
        FurnitureDesign? furnitureDesign = null;
        var valuesDictionary = new Dictionary<Point3, int>();
        var point = start.Point;
        var point2 = start.Point;
        var startValue = SubsystemTerrain.Terrain.GetCellValue(start.Point.X, start.Point.Y, start.Point.Z);
        var num = Terrain.ExtractContents(startValue);
        if (BlocksManager.Blocks[num] is FurnitureBlock)
        {
            var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(startValue));
            furnitureDesign = GetDesign(designIndex);
            if (furnitureDesign == null)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get(_typeName, 0),
                    Color.White, true, false);
                return;
            }

            design = furnitureDesign.Clone();
            design.LinkedDesign = null;
            design.InteractionMode = FurnitureInteractionMode.None;
            valuesDictionary.Add(start.Point, startValue);
        }
        else
        {
            var val = new Stack<Point3>();
            val.Push(start.Point);
            while (val.Count > 0)
            {
                var key = val.Pop();
                if (valuesDictionary.ContainsKey(key))
                {
                    continue;
                }

                var cellValue = SubsystemTerrain.Terrain.GetCellValue(key.X, key.Y, key.Z);
                if (IsValueDisallowed(cellValue))
                {
                    componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get(_typeName, 1),
                        Color.White, true, false);
                    return;
                }

                if (!IsValueAllowed(cellValue))
                {
                    continue;
                }

                if (key.X < point.X)
                {
                    point.X = key.X;
                }

                if (key.Y < point.Y)
                {
                    point.Y = key.Y;
                }

                if (key.Z < point.Z)
                {
                    point.Z = key.Z;
                }

                if (key.X > point2.X)
                {
                    point2.X = key.X;
                }

                if (key.Y > point2.Y)
                {
                    point2.Y = key.Y;
                }

                if (key.Z > point2.Z)
                {
                    point2.Z = key.Z;
                }

                if (MathUtils.Abs(point.X - point2.X) >= 16 || MathUtils.Abs(point.Y - point2.Y) >= 16 ||
                    MathUtils.Abs(point.Z - point2.Z) >= 16)
                {
                    componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get(_typeName, 2),
                        Color.White, true, false);
                    return;
                }

                valuesDictionary[key] = cellValue;
                val.Push(new Point3(key.X - 1, key.Y, key.Z));
                val.Push(new Point3(key.X + 1, key.Y, key.Z));
                val.Push(new Point3(key.X, key.Y - 1, key.Z));
                val.Push(new Point3(key.X, key.Y + 1, key.Z));
                val.Push(new Point3(key.X, key.Y, key.Z - 1));
                val.Push(new Point3(key.X, key.Y, key.Z + 1));
            }

            if (valuesDictionary.Count == 0)
            {
                componentMiner.ComponentPlayer?.ComponentGui.DisplaySmallMessage(LanguageControl.Get(_typeName, 0),
                    Color.White, true, false);
                return;
            }

            design = new FurnitureDesign(SubsystemTerrain);
            var point3 = point2 - point;
            var num2 = MathUtils.Max(MathUtils.Max(point3.X, point3.Y, point3.Z) + 1, 2);
            var array = new int[num2 * num2 * num2];
            foreach (var item in valuesDictionary)
            {
                var point4 = item.Key - point;
                array[point4.X + point4.Y * num2 + point4.Z * num2 * num2] = item.Value;
            }

            design.SetValues(num2, array);
            var steps = start.Face > 3 ? CellFace.Vector3ToFace(direction, 3) : CellFace.OppositeFace(start.Face);
            design.Rotate(1, steps);
            var location = design.Box.Location;
            var point5 = new Point3(design.Resolution) - (design.Box.Location + design.Box.Size);
            var delta = new Point3((point5.X - location.X) / 2, -location.Y, (point5.Z - location.Z) / 2);
            design.Shift(delta);
        }

        var dialog = new BuildFurnitureDialog(
            design,
            furnitureDesign, delegate(bool result)
            {
                if (!result)
                {
                    return;
                }

                if (CommonLib.WorkType != WorkType.Client)
                {
                    CreateDesign(componentMiner, design, valuesDictionary, start, startValue);
                }

                CommonLib.Net.QueuePackage(
                    new FurniturePackage(
                        design,
                        valuesDictionary,
                        start,
                        startValue,
                        CommonLib.WorkType == WorkType.Client)
                );
            });
        DialogsManager.ShowDialog(componentMiner.ComponentPlayer?.GuiWidget, dialog);
    }

    public FurnitureDesign CreateDesign(
        ComponentMiner componentMiner,
        FurnitureDesign design,
        Dictionary<Point3, int> valuesDictionary,
        CellFace start,
        int startValue,
        bool tryAdd = true
    )
    {
        if (tryAdd)
        {
            design = TryAddDesign(design);
        }
        else
        {
            AddDesign(design.Index, design);
        }

        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative)
        {
            foreach (var item2 in valuesDictionary)
            {
                SubsystemTerrain.DestroyCell(0, item2.Key.X, item2.Key.Y, item2.Key.Z, 0, true, true,
                    componentMiner);
            }
        }

        if (componentMiner.ComponentPlayer != null)
        {
            var num3 = AddPickable(componentMiner.ComponentPlayer, design);
            for (var i = 0; i < 3; i++)
            {
                Time.QueueTimeDelayedExecution(Time.FrameStartTime + i * 0.25f,
                    delegate { _subsystemSoundMaterials.PlayImpactSound(startValue, new Vector3(start.Point), 1f); });
            }

            if (componentMiner.ComponentCreature.PlayerStats != null)
            {
                componentMiner.ComponentCreature.PlayerStats.FurnitureItemsMade += num3;
            }
        }

#if DEBUG
        Log.Information("创建家具：Index" + design.Index);
#endif

        return design;
    }

    private int AddPickable(ComponentPlayer player, FurnitureDesign design)
    {
        var componentMiner = player.ComponentMiner;
        var value = Terrain.MakeBlockValue(227, 0,
            FurnitureBlock.SetDesignIndex(0, design.Index, design.ShadowStrengthFactor, design.IsLightEmitter));
        var num3 = MathUtils.Clamp(design.Resolution, 4, 8);
        var matrix = componentMiner.ComponentCreature.ComponentBody.Matrix;
        var position = matrix.Translation + 1f * matrix.Forward + 1f * Vector3.UnitY;
        _subsystemPickables.AddPickable(value, num3, position, null, null);
        componentMiner.DamageActiveTool(1);
        componentMiner.Poke(false);
        return num3;
    }

    public void SwitchToNextState(int x, int y, int z, bool playSound)
    {
        var hashSet = new HashSet<Point3>();
        var list = new List<Point3>
        {
            new(x, y, z)
        };
        var num = 0;
        while (num < list.Count && num < 4096)
        {
            var item = list[num++];
            if (!hashSet.Add(item))
            {
                continue;
            }

            var cellValue = SubsystemTerrain.Terrain.GetCellValue(item.X, item.Y, item.Z);
            if (Terrain.ExtractContents(cellValue) != FurnitureBlock.Index)
            {
                continue;
            }

            var data = Terrain.ExtractData(cellValue);
            var designIndex = FurnitureBlock.GetDesignIndex(data);
            var design = GetDesign(designIndex);
            if (design is not { LinkedDesign.Index: >= 0 } ||
                (list.Count != 1 &&
                 design.InteractionMode != FurnitureInteractionMode
                     .ConnectedMultistate))
            {
                continue;
            }

            var data2 = FurnitureBlock.SetDesignIndex(data, design.LinkedDesign.Index,
                design.LinkedDesign.ShadowStrengthFactor, design.LinkedDesign.IsLightEmitter);
            var value = Terrain.ReplaceData(cellValue, data2);
            SubsystemTerrain.ChangeCell(item.X, item.Y, item.Z, value);
            if (design.InteractionMode != FurnitureInteractionMode.ConnectedMultistate)
            {
                continue;
            }

            list.Add(new Point3(item.X - 1, item.Y, item.Z));
            list.Add(new Point3(item.X + 1, item.Y, item.Z));
            list.Add(new Point3(item.X, item.Y - 1, item.Z));
            list.Add(new Point3(item.X, item.Y + 1, item.Z));
            list.Add(new Point3(item.X, item.Y, item.Z - 1));
            list.Add(new Point3(item.X, item.Y, item.Z + 1));
        }

        if (playSound)
        {
            _subsystemAudio.PlaySound("Audio/BlockPlaced", 1f, 0f, new Vector3(x, y, z), 2f, true);
        }
    }

    /// <summary>
    /// 遍历背包方块，如果不存在，则让它为null
    /// </summary>
    public void GarbageCollectDesigns()
    {
        GarbageCollectDesigns(_subsystemItemsScanner.ScanItems());
    }

    public FurnitureSet NewFurnitureSet(string name, string importedFrom)
    {
        if (name.Length > MaxFurnitureSetNameLength)
        {
            name = name[..MaxFurnitureSetNameLength];
        }

        var num = 0;
        while (FurnitureSets.FirstOrDefault(fs => fs.Name == name) != null)
        {
            num++;
            name = num > 0 ? name + num.ToString(CultureInfo.InvariantCulture) : name;
        }

        var furnitureSet = new FurnitureSet
        {
            Name = name,
            ImportedFrom = importedFrom
        };
        _furnitureSets.Add(furnitureSet);
        return furnitureSet;
    }

    public void DeleteFurnitureSet(FurnitureSet furnitureSet)
    {
        foreach (var furnitureSetDesign in GetFurnitureSetDesigns(furnitureSet))
        {
            furnitureSetDesign?.FurnitureSet = FurnitureSetDefault.Default;
        }

        _furnitureSets.Remove(furnitureSet);
    }

    public void MoveFurnitureSet(FurnitureSet furnitureSet, int move)
    {
        var num = _furnitureSets.IndexOf(furnitureSet);
        if (num < 0)
        {
            return;
        }

        _furnitureSets.RemoveAt(num);
        _furnitureSets.Insert(MathUtils.Clamp(num + move, 0, _furnitureSets.Count), furnitureSet);
    }

    public void AddToFurnitureSet(FurnitureDesign design, FurnitureSet furnitureSet)
    {
        foreach (var item in design.ListChain())
        {
            item.FurnitureSet = furnitureSet;
        }
    }

    public IEnumerable<FurnitureDesign?> GetFurnitureSetDesigns(FurnitureSet furnitureSet)
    {
        return FurnitureDesigns.Where(fd => fd != null && fd.FurnitureSet == furnitureSet);
    }

    public static List<FurnitureDesign> LoadFurnitureDesigns(
        SubsystemTerrain? subsystemTerrain,
        ValuesDictionary valuesDictionary
    )
    {
        var list = new List<FurnitureDesign>();
        foreach (var item2 in valuesDictionary)
        {
            var index = int.Parse(item2.Key, CultureInfo.InvariantCulture);
            var valuesDictionary2 = (ValuesDictionary)item2.Value;
            var item = new FurnitureDesign(index, subsystemTerrain, valuesDictionary2);
            list.Add(item);
        }

        foreach (var design in list)
        {
            if (design.LoadTimeLinkedDesignIndex >= 0)
            {
                design.LinkedDesign = list.FirstOrDefault(d => d.Index == design.LoadTimeLinkedDesignIndex);
            }
        }

        return list;
    }

    public static void SaveFurnitureDesigns(ValuesDictionary valuesDictionary, ICollection<FurnitureDesign?> designs)
    {
        foreach (var design in designs)
        {
            if (design != null)
            {
                valuesDictionary.SetValue(design.Index.ToString(CultureInfo.InvariantCulture), design.Save());
            }
        }
    }

    public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
    {
        AddTerrainFurniture(value);
        AddParticleSystems(value, x, y, z);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveTerrainFurniture(value);
        RemoveParticleSystems(x, y, z);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        RemoveTerrainFurniture(oldValue);
        RemoveParticleSystems(x, y, z);
        AddTerrainFurniture(value);
        AddParticleSystems(value, x, y, z);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        if (!isLoaded)
        {
            AddTerrainFurniture(value);
        }

        AddParticleSystems(value, x, y, z);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var list = new List<Point3>();
        foreach (var key in _particleSystemsByCell.Keys)
        {
            if (key.X >= chunk.Origin.X && key.X < chunk.Origin.X + 16 && key.Z >= chunk.Origin.Y &&
                key.Z < chunk.Origin.Y + 16)
            {
                list.Add(key);
            }
        }

        foreach (var item in list)
        {
            RemoveParticleSystems(item.X, item.Y, item.Z);
        }
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(raycastResult.CellFace.X, raycastResult.CellFace.Y,
            raycastResult.CellFace.Z);
        if (Terrain.ExtractContents(cellValue) == 227)
        {
            var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(cellValue));
            var design = GetDesign(designIndex);
            if (design != null && (design.InteractionMode == FurnitureInteractionMode.Multistate ||
                                   design.InteractionMode == FurnitureInteractionMode.ConnectedMultistate))
            {
                SwitchToNextState(raycastResult.CellFace.X, raycastResult.CellFace.Y, raycastResult.CellFace.Z, true);
                return true;
            }
        }

        return false;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _subsystemItemsScanner = Project.FindSubsystem<SubsystemItemsScanner>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        var value = valuesDictionary.GetValue<ValuesDictionary>("FurnitureDesigns");
        foreach (var item in LoadFurnitureDesigns(SubsystemTerrain, value))
        {
            FurnitureDesigns[item.Index] = item;
        }

        foreach (ValuesDictionary item2 in valuesDictionary.GetValue<ValuesDictionary>("FurnitureSets").Values
                     .Where(v => v is ValuesDictionary))
        {
            var value2 = item2.GetValue<string>("Name");
            var value3 = item2.GetValue("ImportedFrom", string.Empty);
            var value4 = item2.GetValue<string>("Indices");
            var array = HumanReadableConverter.ValuesListFromString<int>(';', value4);
            var furnitureSet = new FurnitureSet
            {
                Name = value2,
                ImportedFrom = value3
            };
            _furnitureSets.Add(furnitureSet);
            foreach (var num in array)
            {
                if (num >= 0 && num < FurnitureDesigns.Length && FurnitureDesigns[num] != null)
                {
                    FurnitureDesigns[num]!.FurnitureSet = furnitureSet;
                }
            }
        }

        _subsystemItemsScanner.ItemsScanned += GarbageCollectDesigns;
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        base.Save(valuesDictionary);
        GarbageCollectDesigns();
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("FurnitureDesigns", valuesDictionary2);
        SaveFurnitureDesigns(valuesDictionary2, FurnitureDesigns.Where(d => d != null).ToArray());
        var valuesDictionary3 = new ValuesDictionary();
        valuesDictionary.SetValue("FurnitureSets", valuesDictionary3);
        var num = 0;
        foreach (var furnitureSet in FurnitureSets)
        {
            var valuesDictionary4 = new ValuesDictionary();
            valuesDictionary3.SetValue(num.ToString(CultureInfo.InvariantCulture), valuesDictionary4);
            valuesDictionary4.SetValue("Name", furnitureSet.Name);
            if (!string.IsNullOrEmpty(furnitureSet.ImportedFrom))
            {
                valuesDictionary4.SetValue("ImportedFrom", furnitureSet.ImportedFrom);
            }

            var value = HumanReadableConverter.ValuesListToString(';', (from d in GetFurnitureSetDesigns(furnitureSet)
                select d.Index).ToArray());
            valuesDictionary4.SetValue("Indices", value);
            num++;
        }
    }

    private void AddDesign(int index, FurnitureDesign design)
    {
        FurnitureDesigns[index] = design;
        design.Index = index;
        design.TerrainUseCount = 0;
    }

    private void AddTerrainFurniture(int value)
    {
        if (Terrain.ExtractContents(value) != 227)
        {
            return;
        }

        var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value));
        if (designIndex < FurnitureDesigns.Length)
        {
            FurnitureDesigns[designIndex]!.TerrainUseCount++;
        }
    }

    private void RemoveTerrainFurniture(int value)
    {
        if (Terrain.ExtractContents(value) != 227)
        {
            return;
        }

        var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value));
        if (designIndex < FurnitureDesigns.Length)
        {
            FurnitureDesigns[designIndex]!.TerrainUseCount =
                MathUtils.Max(FurnitureDesigns[designIndex]!.TerrainUseCount - 1, 0);
        }
    }

    private void GarbageCollectDesigns(ReadOnlyList<ScannedItemData> allExistingItems)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        foreach (var furnitureDesign in FurnitureDesigns)
        {
            if (furnitureDesign != null)
            {
                furnitureDesign.GcUsed = furnitureDesign.TerrainUseCount > 0;
            }
        }

        foreach (var item in allExistingItems)
        {
            if (Terrain.ExtractContents(item.Value) == FurnitureBlock.Index)
            {
                var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(item.Value));
                var design = GetDesign(designIndex);
                design?.GcUsed = true;
            }
        }

        foreach (var furnitureDesign in FurnitureDesigns)
        {
            if (furnitureDesign is { GcUsed: true })
            {
                var linkedDesign = furnitureDesign.LinkedDesign;
                while (linkedDesign is { GcUsed: false })
                {
                    linkedDesign.GcUsed = true;
                    linkedDesign = linkedDesign.LinkedDesign;
                }
            }
        }

        var list = new List<int>();
        for (var k = 0; k < FurnitureDesigns.Length; k++)
        {
            if (FurnitureDesigns[k] != null && !FurnitureDesigns[k]!.GcUsed &&
                FurnitureDesigns[k]!.FurnitureSet is FurnitureSetDefault)
            {
                var item = FurnitureDesigns[k]!;
                list.Add(item.Index);
                item.Index = -1;
                FurnitureDesigns[k] = null;
            }
        }

        if (CommonLib.WorkType == WorkType.Server)
        {
            CommonLib.Net.QueuePackage(new FurniturePackage(list));
        }
    }

    private static bool IsValueAllowed(int value)
    {
        var contents = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[contents];
        return block.GetFurnitureBuilt(value);
    }

    private static bool IsValueDisallowed(int value)
    {
        var num = Terrain.ExtractContents(value);
        var data = Terrain.ExtractData(value);
        return num is 18 or 92 && FluidBlock.GetLevel(data) != 0 && FluidBlock.GetIsTop(data);
    }

    private void AddParticleSystems(int value, int x, int y, int z)
    {
        if (Terrain.ExtractContents(value) != FurnitureBlock.Index)
        {
            return;
        }

        var data = Terrain.ExtractData(value);
        var rotation = FurnitureBlock.GetRotation(data);
        var designIndex = FurnitureBlock.GetDesignIndex(data);
        var design = GetDesign(designIndex);
        if (design == null)
        {
            return;
        }

        var list = new List<FireParticleSystem>();
        var torchPoints = design.GetTorchPoints(rotation);
        if (torchPoints.Length != 0)
        {
            foreach (var boundingBox in torchPoints)
            {
                var num = (boundingBox.Size().X + boundingBox.Size().Y + boundingBox.Size().Z) / 3f;
                var size = MathUtils.Clamp(1.5f * num, 0.1f, 1f);
                var fireParticleSystem = new FireParticleSystem(new Vector3(x, y, z) + boundingBox.Center(), size, 24f);
                _subsystemParticles.AddParticleSystem(fireParticleSystem);
                list.Add(fireParticleSystem);
            }
        }

        if (list.Count > 0)
        {
            _particleSystemsByCell[new Point3(x, y, z)] = list;
        }
    }

    private void RemoveParticleSystems(int x, int y, int z)
    {
        if (!_particleSystemsByCell.TryGetValue(new Point3(x, y, z), out var value))
        {
            return;
        }

        foreach (var item in value)
        {
            item.IsStopped = true;
        }

        _particleSystemsByCell.Remove(new Point3(x, y, z));
    }
}
