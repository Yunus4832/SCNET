using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Modding.Blocks;
using Game.Network.Handling;

namespace Game.Subsystems;

public sealed class SubsystemFurniturePresentation : Subsystem, IUpdateable
{
    private const string _typeName = nameof(SubsystemFurnitureBlockBehavior);

    private readonly Dictionary<Point3, int> _cells = new();

    private readonly Dictionary<Point3, List<FireParticleSystem>> _particleSystemsByCell = new();

    private readonly Dictionary<Point3, (FurnitureDesign? Design, long Revision, int Value)> _applied = new();

    private readonly HashSet<FireParticleSystem> _retiring = [];

    private SubsystemFurnitureBlockBehavior _furniture = null!;

    private SubsystemTerrain _terrain = null!;

    private SubsystemParticles _particles = null!;

    private bool _disposed;

    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _furniture = Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _furniture.ScanRequested += OnScanRequested;
        _furniture.FurnitureCellChanged += OnCellChanged;
        _furniture.FurnitureChunkDiscarded += OnChunkDiscarded;
        _furniture.DesignTableChanged += RefreshDesigns;
        _terrain.Closing += OnTerrainClosing;
        foreach (var chunk in _terrain.Terrain.AllocatedChunks)
        {
            for (var z = 0; z < 16; z++)
            {
                for (var x = 0; x < 16; x++)
                {
                    for (var y = 0; y < 256; y++)
                    {
                        var value = chunk.GetCellValueFast(x, y, z);
                        if (Terrain.ExtractContents(value) == BuiltInBlockRegistry.GetIndex<FurnitureBlock>())
                        {
                            OnCellChanged(new Point3(chunk.Origin.X + x, y, chunk.Origin.Y + z), value);
                        }
                    }
                }
            }
        }
    }

    public void Update(float dt)
    {
        _retiring.RemoveWhere(particles => particles.SubsystemParticles is null);
    }

    private void OnScanRequested(FurnitureScanRequest request)
    {
        if (_disposed || !CanUseActor(Project, request.Miner)) { return; }

        ScanDesign(request.Start, request.Direction, request.Miner);
    }

    internal static FurnitureDesign? CopySourceDesign(FurnitureDesign? source) => source?.Clone();

    private void SubmitSourceRename(ComponentMiner miner, CellFace start, int startValue, string name)
    {
        if (_disposed || !CanUseActor(Project, miner)) { return; }

        var expectedIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(startValue));
        var current = _terrain.Terrain.GetCellValue(start.X, start.Y, start.Z);
        if (Terrain.ExtractContents(current) != BuiltInBlockRegistry.GetIndex<FurnitureBlock>() ||
            FurnitureBlock.GetDesignIndex(Terrain.ExtractData(current)) != expectedIndex ||
            _furniture.GetDesign(expectedIndex) is not { } source)
        {
            return;
        }

        var validationCopy = source.Clone();
        validationCopy.Name = name;
        FurniturePackageHandler.Submit(
            Project,
            new FurnitureDesignRenameRequestPackage
            {
                Source = start, FurnitureIndex = expectedIndex, Name = validationCopy.Name
            }
        );
    }

    internal static bool CanUseActor(Project project, ComponentMiner miner)
    {
        return WorldRuntimeContext.IsProjectActive(project) &&
               ReferenceEquals(miner.Project, project) &&
               miner.ComponentCharacter is { IsLocallyControlled: true };
    }

    internal static bool CanSubmitScan(
        Project project,
        SubsystemTerrain terrain,
        ComponentMiner miner,
        IReadOnlyDictionary<Point3, int> cells
    )
    {
        return CanUseActor(project, miner) &&
               cells.Count is > 0 and <= 4096 &&
               cells.All(cell => terrain.Terrain.GetCellValue(cell.Key.X, cell.Key.Y, cell.Key.Z) == cell.Value);
    }

    private void OnCellChanged(Point3 point, int value)
    {
        if (_disposed) { return; }

        if (Terrain.ExtractContents(value) != BuiltInBlockRegistry.GetIndex<FurnitureBlock>())
        {
            _cells.Remove(point);
            _applied.Remove(point);
            RemoveParticleSystems(point.X, point.Y, point.Z);
            return;
        }

        _cells[point] = value;
        RefreshCell(point, value);
    }

    private void RefreshCell(Point3 point, int value)
    {
        var design = _furniture.GetDesign(FurnitureBlock.GetDesignIndex(Terrain.ExtractData(value)));
        var stamp = (design, design?.Revision ?? -1, value);
        if (_applied.TryGetValue(point, out var previous) && previous == stamp) { return; }

        RemoveParticleSystems(point.X, point.Y, point.Z);
        _applied[point] = stamp;
        AddParticleSystems(value, point.X, point.Y, point.Z);
    }

    private void RefreshDesigns()
    {
        if (_disposed) { return; }

        foreach (var cell in _cells) { RefreshCell(cell.Key, cell.Value); }
    }

    private void OnChunkDiscarded(Point2 chunk)
    {
        foreach (var point in _cells.Keys.Where(point => point.X >> 4 == chunk.X && point.Z >> 4 == chunk.Y).ToArray())
        {
            _cells.Remove(point);
            _applied.Remove(point);
            RemoveParticleSystems(point.X, point.Y, point.Z);
        }
    }

    private void OnTerrainClosing(SubsystemTerrain terrain) => Dispose();

    private void ScanDesign(CellFace start, Vector3 direction, ComponentMiner componentMiner)
    {
        FurnitureDesign? design;
        FurnitureDesign? furnitureDesign = null;
        var valuesDictionary = new Dictionary<Point3, int>();
        var point = start.Point;
        var point2 = start.Point;
        var startValue = _terrain.Terrain.GetCellValue(start.Point.X, start.Point.Y, start.Point.Z);
        var num = Terrain.ExtractContents(startValue);
        if (WorldContentBinding.For(Project).Catalog.Blocks[num] is FurnitureBlock)
        {
            var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(startValue));
            furnitureDesign = CopySourceDesign(_furniture.GetDesign(designIndex));
            if (furnitureDesign == null)
            {
                componentMiner.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(_typeName, 0),
                    Color.White,
                    true,
                    false
                );
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

                var cellValue = _terrain.Terrain.GetCellValue(key.X, key.Y, key.Z);
                if (IsValueDisallowed(cellValue))
                {
                    componentMiner.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                        Localization.ClientLanguage.Catalog.Get(_typeName, 1),
                        Color.White,
                        true,
                        false
                    );
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

                if (MathUtils.Abs(point.X - point2.X) >= 16 ||
                    MathUtils.Abs(point.Y - point2.Y) >= 16 ||
                    MathUtils.Abs(point.Z - point2.Z) >= 16)
                {
                    componentMiner.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                        Localization.ClientLanguage.Catalog.Get(_typeName, 2),
                        Color.White,
                        true,
                        false
                    );
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
                componentMiner.ComponentCharacter?.Entity.FindComponent<ComponentGui>(true)!.DisplaySmallMessage(
                    Localization.ClientLanguage.Catalog.Get(_typeName, 0),
                    Color.White,
                    true,
                    false
                );
                return;
            }

            design = new FurnitureDesign(_terrain.Catalog, _terrain);
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
            Project.FindSubsystem<SubsystemBlockPresentation>(true)!.Catalog,
            _terrain,
            design,
            furnitureDesign,
            delegate(bool result)
            {
                if (!result)
                {
                    return;
                }

                if (!CanSubmitScan(Project, _terrain, componentMiner, valuesDictionary)) { return; }

                var package = FurniturePackageBuilder.Capture(design, valuesDictionary, start, startValue, true);
                FurniturePackageHandler.Submit(Project, package);
            },
            furnitureDesign is null ? null : name => SubmitSourceRename(componentMiner, start, startValue, name)
        );
        DialogsManager.ShowDialog(
            componentMiner.ComponentCharacter?.Entity.FindComponent<ComponentCharacterPresentation>(true)?.GuiWidget,
            dialog
        );
    }

    private bool IsValueAllowed(int value)
    {
        var contents = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
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
        if (Terrain.ExtractContents(value) != BuiltInBlockRegistry.GetIndex<FurnitureBlock>())
        {
            return;
        }

        var data = Terrain.ExtractData(value);
        var rotation = FurnitureBlock.GetRotation(data);
        var designIndex = FurnitureBlock.GetDesignIndex(data);
        var design = _furniture.GetDesign(designIndex);
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
                _particles.AddParticleSystem(fireParticleSystem);
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
            _retiring.Add(item);
        }

        _particleSystemsByCell.Remove(new Point3(x, y, z));
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_furniture is not null)
        {
            _furniture.ScanRequested -= OnScanRequested;
            _furniture.FurnitureCellChanged -= OnCellChanged;
            _furniture.FurnitureChunkDiscarded -= OnChunkDiscarded;
            _furniture.DesignTableChanged -= RefreshDesigns;
        }

        if (_terrain is not null) { _terrain.Closing -= OnTerrainClosing; }

        foreach (var particles in _particleSystemsByCell.Values.SelectMany(list => list).Concat(_retiring).Distinct())
        {
            particles.IsStopped = true;
            if (ReferenceEquals(particles.SubsystemParticles, _particles))
            {
                _particles.RemoveParticleSystem(particles);
            }
        }

        _particleSystemsByCell.Clear();
        _retiring.Clear();
        _cells.Clear();
        _applied.Clear();
        base.Dispose();
    }
}
