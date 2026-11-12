using System.Globalization;

using Engine.Core.Serialization;

using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems.Behavior;

public class SubsystemFurnitureBlockBehavior : SubsystemBlockBehavior
{
    public const int MaxFurnitureSetNameLength = 64;

    public readonly FurnitureDesign?[] FurnitureDesigns = new FurnitureDesign[ComponentFurnitureInventory.MaxDesign];

    private readonly List<FurnitureSet> _furnitureSets = [];

    private SubsystemSpatialSoundRequests _soundRequests = null!;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    private SubsystemItemsScanner _subsystemItemsScanner = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemSoundMaterials _subsystemSoundMaterials = null!;

    public override ResourceId[] HandledBlocks => [];

    public ReadOnlyList<FurnitureSet> FurnitureSets => new(_furnitureSets);

    public int NetworkRevision { get; private set; }

    public void NotifyNetworkChange()
    {
        NetworkRevision++;
        DesignTableChanged?.Invoke();
    }

    public void InstallNetworkDesign(FurnitureDesign design)
    {
        AddDesign(design.Index, design);
    }

    public bool TryRenameDesign(int index, string name)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority ||
            name is null ||
            name.Length > 20 ||
            GetDesign(index) is not { } design)
        {
            return false;
        }

        try { design.Name = name; }
        catch (InvalidOperationException) { return false; }

        NotifyNetworkChange();
        return true;
    }

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

        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return null;
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

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            NetworkSender.SendGlobal(Project, FurniturePackageBuilder.Capture(design));
        }

        return design;
    }

    public event Action<FurnitureScanRequest>? ScanRequested;

    public event Action<Point3, int>? FurnitureCellChanged;

    public event Action<Point2>? FurnitureChunkDiscarded;

    public event Action? DesignTableChanged;

    public void RequestDesignScan(CellFace start, Vector3 direction, ComponentMiner miner)
    {
        if (ReferenceEquals(miner.Project, Project) && miner.ComponentCharacter is { IsLocallyControlled: true })
        {
            ScanRequested?.Invoke(new FurnitureScanRequest(start, direction, miner));
        }
    }

    public FurnitureDesign CreateDesign(
        ComponentMiner componentMiner,
        FurnitureDesign design,
        IReadOnlyDictionary<Point3, int> valuesDictionary,
        CellFace start,
        int startValue
    )
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            throw new InvalidOperationException("Only the authority can create furniture designs.");
        }

        design = TryAddDesign(design);

        if (_subsystemGameInfo.WorldSettings.GameMode != GameMode.Creative)
        {
            foreach (var item2 in valuesDictionary)
            {
                SubsystemTerrain.DestroyCell(
                    0,
                    item2.Key.X,
                    item2.Key.Y,
                    item2.Key.Z,
                    0,
                    true,
                    true,
                    componentMiner
                );
            }
        }

        if (componentMiner.ComponentCharacter is not null)
        {
            var num3 = AddPickable(componentMiner.ComponentCharacter, design);
            for (var i = 0; i < 3; i++)
            {
                WorldRuntimeContext.For(Project).QueueTimeDelayedExecution(
                    Time.FrameStartTime + i * 0.25f,
                    delegate { _subsystemSoundMaterials.PlayImpactSound(startValue, new Vector3(start.Point), 1f); }
                );
            }

            componentMiner.ComponentCreature.CharacterStats?.FurnitureItemsMade += num3;
        }

        Log.Debug("创建家具：Index" + design.Index);
        return design;
    }

    private int AddPickable(ComponentCharacter player, FurnitureDesign design)
    {
        var componentMiner = player.ComponentMiner;
        var value = Terrain.MakeBlockValue(
            227,
            FurnitureBlock.SetDesignIndex(0, design.Index, design.ShadowStrengthFactor, design.IsLightEmitter)
        );
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
            if (Terrain.ExtractContents(cellValue) != BuiltInBlockRegistry.GetIndex<FurnitureBlock>())
            {
                continue;
            }

            var data = Terrain.ExtractData(cellValue);
            var designIndex = FurnitureBlock.GetDesignIndex(data);
            var design = GetDesign(designIndex);
            if (design is not { LinkedDesign.Index: >= 0 } ||
                (list.Count != 1 &&
                 design.InteractionMode !=
                 FurnitureInteractionMode
                     .ConnectedMultistate))
            {
                continue;
            }

            var data2 = FurnitureBlock.SetDesignIndex(
                data,
                design.LinkedDesign.Index,
                design.LinkedDesign.ShadowStrengthFactor,
                design.LinkedDesign.IsLightEmitter
            );
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
            _soundRequests.RequestSound("Audio/BlockPlaced", 1f, 0f, new Vector3(x, y, z), 2f, true);
        }
    }

    /// <summary>
    ///     遍历背包方块，如果不存在，则让它为null
    /// </summary>
    public void GarbageCollectDesigns()
    {
        GarbageCollectDesigns(_subsystemItemsScanner.ScanItems());
    }

    public FurnitureSet NewFurnitureSet(string name, string importedFrom)
    {
        name = GetAvailableFurnitureSetName(name, null);

        var furnitureSet = new FurnitureSet
        {
            Name = name,
            ImportedFrom = importedFrom
        };
        _furnitureSets.Add(furnitureSet);
        return furnitureSet;
    }

    public void RenameFurnitureSet(FurnitureSet furnitureSet, string name)
    {
        furnitureSet.Name = GetAvailableFurnitureSetName(name, furnitureSet);
    }

    private string GetAvailableFurnitureSetName(string name, FurnitureSet? excluded)
    {
        var baseName = name[..Math.Min(name.Length, MaxFurnitureSetNameLength)];
        var candidate = baseName;
        for (var suffix = 1;
             _furnitureSets.Any(set => !ReferenceEquals(set, excluded) && set.Name == candidate);
             suffix++)
        {
            var number = suffix.ToString(CultureInfo.InvariantCulture);
            candidate = baseName[..Math.Min(baseName.Length, MaxFurnitureSetNameLength - number.Length)] + number;
        }

        return candidate;
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
        BlockContentCatalog? catalog,
        SubsystemTerrain? subsystemTerrain,
        ValuesDictionary valuesDictionary
    )
    {
        var list = new List<FurnitureDesign>();
        foreach (var item2 in valuesDictionary)
        {
            var index = int.Parse(item2.Key, CultureInfo.InvariantCulture);
            var valuesDictionary2 = (ValuesDictionary)item2.Value;
            var item = new FurnitureDesign(index, catalog, subsystemTerrain, valuesDictionary2);
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
        FurnitureCellChanged?.Invoke(new Point3(x, y, z), value);
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        RemoveTerrainFurniture(value);
        FurnitureCellChanged?.Invoke(new Point3(x, y, z), newValue);
    }

    public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
    {
        RemoveTerrainFurniture(oldValue);
        AddTerrainFurniture(value);
        FurnitureCellChanged?.Invoke(new Point3(x, y, z), value);
    }

    public override void OnBlockGenerated(int value, int x, int y, int z, bool isLoaded)
    {
        if (!isLoaded)
        {
            AddTerrainFurniture(value);
        }

        FurnitureCellChanged?.Invoke(new Point3(x, y, z), value);
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        FurnitureChunkDiscarded?.Invoke(chunk.Coords);
    }

    public override bool OnInteract(TerrainRaycastResult raycastResult, ComponentMiner componentMiner)
    {
        var cellValue = SubsystemTerrain.Terrain.GetCellValue(
            raycastResult.CellFace.X,
            raycastResult.CellFace.Y,
            raycastResult.CellFace.Z
        );
        if (Terrain.ExtractContents(cellValue) == 227)
        {
            var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(cellValue));
            var design = GetDesign(designIndex);
            if (design != null &&
                (design.InteractionMode == FurnitureInteractionMode.Multistate ||
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
        _soundRequests = Project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!;
        _subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true)!;
        _subsystemItemsScanner = Project.FindSubsystem<SubsystemItemsScanner>(true)!;
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        var value = valuesDictionary.GetValue<ValuesDictionary>("FurnitureDesigns");
        foreach (var item in LoadFurnitureDesigns(SubsystemTerrain.Catalog, SubsystemTerrain, value))
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

            var value = HumanReadableConverter.ValuesListToString(
                ';',
                (from d in GetFurnitureSetDesigns(furnitureSet)
                    select d.Index).ToArray()
            );
            valuesDictionary4.SetValue("Indices", value);
            num++;
        }
    }

    private void AddDesign(int index, FurnitureDesign design)
    {
        FurnitureDesigns[index] = design;
        design.Index = index;
        design.TerrainUseCount = 0;
        DesignTableChanged?.Invoke();
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
        if (!WorldRuntimeContext.For(Project).IsAuthority)
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

        var inventoryValues = allExistingItems.Select(item => item.Value)
            .Concat(Project.FindSubsystem<SubsystemCharacters>(true)!.GetOfflineInventoryValues());
        foreach (var value in inventoryValues)
        {
            if (Terrain.ExtractContents(value) == BuiltInBlockRegistry.GetIndex<FurnitureBlock>())
            {
                var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value));
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
            if (FurnitureDesigns[k] != null &&
                !FurnitureDesigns[k]!.GcUsed &&
                FurnitureDesigns[k]!.FurnitureSet is FurnitureSetDefault)
            {
                var item = FurnitureDesigns[k]!;
                list.Add(item.Index);
                item.Index = -1;
                FurnitureDesigns[k] = null;
            }
        }

        if (list.Count > 0) { DesignTableChanged?.Invoke(); }

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            NetworkSender.Send(
                Project,
                new FurnitureDesignsRemovedPackage { ToRemoveList = list.ToArray() },
                PackageAudience.Global
            );
        }
    }

    public override void Dispose()
    {
        if (_subsystemItemsScanner is not null) { _subsystemItemsScanner.ItemsScanned -= GarbageCollectDesigns; }

        ScanRequested = null;
        FurnitureCellChanged = null;
        FurnitureChunkDiscarded = null;
        DesignTableChanged = null;
        base.Dispose();
    }
}
