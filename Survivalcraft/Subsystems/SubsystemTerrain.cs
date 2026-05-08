using Engine.Graphics;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;
using Game.TerrainSerializers;
using Game.TerrainSerializers.NetWork;

namespace Game.Subsystems;

public class SubsystemTerrain : Subsystem, IDrawable, IUpdateable
{
    public static bool TerrainRenderingEnabled = true;

    private readonly Dictionary<Point3, bool> _modifiedCells = new();

    private readonly DynamicArray<Point3> _modifiedList = [];

    private static readonly Point3[] _neighborOffsets =
    [
        new(0, 0, 0),
        new(-1, 0, 0),
        new(1, 0, 0),
        new(0, -1, 0),
        new(0, 1, 0),
        new(0, 0, -1),
        new(0, 0, 1)
    ];

    private SubsystemGameWidgets _subsystemViews = null!;

    private SubsystemParticles _subsystemParticles = null!;

    private SubsystemPickables _subsystemPickables = null!;

    private SubsystemBlockBehaviors _subsystemBlockBehaviors = null!;

    private readonly List<BlockDropValue> _dropValues = [];

    public PrimitivesRenderer3D PrimitivesRenderer = new();

    private static readonly int[] _drawOrders =
    [
        0,
        100
    ];

    public SubsystemGameInfo SubsystemGameInfo { get; set; } = null!;

    public SubsystemAnimatedTextures SubsystemAnimatedTextures { get; set; } = null!;

    public SubsystemFurnitureBlockBehavior SubsystemFurnitureBlockBehavior { get; set; } = null!;

    public SubsystemPalette SubsystemPalette { get; set; } = null!;

    public Terrain Terrain { get; set; } = null!;

    public TerrainUpdater TerrainUpdater { get; set; } = null!;

    public TerrainRenderer TerrainRenderer { get; set; } = null!;

    public TerrainSerializer24 TerrainSerializer { get; set; } = null!;

    public ITerrainContentsGenerator TerrainContentsGenerator { get; set; } = null!;

    public BlockGeometryGenerator BlockGeometryGenerator { get; set; } = null!;

    public int[] DrawOrders => _drawOrders;

    public UpdateOrder UpdateOrder => UpdateOrder.Terrain;

    public void ProcessModifiedCells()
    {
        _modifiedList.Count = 0;
        foreach (var key in _modifiedCells.Keys)
        {
            _modifiedList.Add(key);
        }

        _modifiedCells.Clear();
        for (var i = 0; i < _modifiedList.Count; i++)
        {
            var point = _modifiedList.Array[i];
            foreach (var point2 in _neighborOffsets)
            {
                var cellContents = Terrain.GetCellContents(point.X + point2.X, point.Y + point2.Y, point.Z + point2.Z);
                var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(cellContents);
                foreach (var behavior in blockBehaviors)
                {
                    behavior.OnNeighborBlockChanged(point.X + point2.X, point.Y + point2.Y, point.Z + point2.Z,
                        point.X, point.Y, point.Z);
                }
            }
        }
    }

    public TerrainRaycastResult? Raycast(
        Vector3 start,
        Vector3 end,
        bool useInteractionBoxes,
        bool skipAirBlocks,
        Func<int, float, bool>? action
    )
    {
        var num = Vector3.Distance(start, end);
        if (num > 1000f)
        {
            Log.Warning("Terrain raycast too long, trimming.");
            end = start + 1000f * Vector3.Normalize(end - start);
        }

        var ray = new Ray3(start, Vector3.Normalize(end - start));
        var x = start.X;
        var y = start.Y;
        var z = start.Z;
        var x2 = end.X;
        var y2 = end.Y;
        var z2 = end.Z;
        var num2 = Terrain.ToCell(x);
        var num3 = Terrain.ToCell(y);
        var num4 = Terrain.ToCell(z);
        var num5 = Terrain.ToCell(x2);
        var num6 = Terrain.ToCell(y2);
        var num7 = Terrain.ToCell(z2);
        var num8 = x < x2 ? 1 : x > x2 ? -1 : 0;
        var num9 = y < y2 ? 1 : y > y2 ? -1 : 0;
        var num10 = z < z2 ? 1 : z > z2 ? -1 : 0;
        var num11 = MathUtils.Floor(x);
        var num12 = num11 + 1f;
        var num13 = (x > x2 ? x - num11 : num12 - x) / Math.Abs(x2 - x);
        var num14 = MathUtils.Floor(y);
        var num15 = num14 + 1f;
        var num16 = (y > y2 ? y - num14 : num15 - y) / Math.Abs(y2 - y);
        var num17 = MathUtils.Floor(z);
        var num18 = num17 + 1f;
        var num19 = (z > z2 ? z - num17 : num18 - z) / Math.Abs(z2 - z);
        var num20 = 1f / Math.Abs(x2 - x);
        var num21 = 1f / Math.Abs(y2 - y);
        var num22 = 1f / Math.Abs(z2 - z);
        while (true)
        {
            BoundingBox boundingBox = default;
            var collisionBoxIndex = 0;
            float? num23 = null;
            var cellValue = Terrain.GetCellValue(num2, num3, num4);
            var num24 = Terrain.ExtractContents(cellValue);
            if (num24 != 0 || !skipAirBlocks)
            {
                var ray2 = new Ray3(ray.Position - new Vector3(num2, num3, num4), ray.Direction);
                var num25 = BlocksManager.Blocks[num24].Raycast(ray2, this, cellValue, useInteractionBoxes,
                    out var nearestBoxIndex, out var nearestBox);
                if (num25.HasValue && (!num23.HasValue || num25.Value < num23.Value))
                {
                    num23 = num25;
                    collisionBoxIndex = nearestBoxIndex;
                    boundingBox = nearestBox;
                }
            }

            if (num23.HasValue && num23.Value <= num && (action == null || action(cellValue, num23.Value)))
            {
                var face = 0;
                var vector = start - new Vector3(num2, num3, num4) + num23.Value * ray.Direction;
                var num26 = float.MaxValue;
                var num27 = MathUtils.Abs(vector.X - boundingBox.Min.X);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 3;
                }

                num27 = MathUtils.Abs(vector.X - boundingBox.Max.X);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 1;
                }

                num27 = MathUtils.Abs(vector.Y - boundingBox.Min.Y);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 5;
                }

                num27 = MathUtils.Abs(vector.Y - boundingBox.Max.Y);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 4;
                }

                num27 = MathUtils.Abs(vector.Z - boundingBox.Min.Z);
                if (num27 < num26)
                {
                    num26 = num27;
                    face = 2;
                }

                num27 = MathUtils.Abs(vector.Z - boundingBox.Max.Z);
                if (num27 < num26)
                {
                    face = 0;
                }

                TerrainRaycastResult value = default;
                value.Ray = ray;
                value.Value = cellValue;
                value.CellFace = new CellFace
                {
                    X = num2,
                    Y = num3,
                    Z = num4,
                    Face = face
                };
                value.CollisionBoxIndex = collisionBoxIndex;
                value.Distance = num23.Value;
                return value;
            }

            if (num13 <= num16 && num13 <= num19)
            {
                if (num2 == num5)
                {
                    break;
                }

                num13 += num20;
                num2 += num8;
            }
            else if (num16 <= num13 && num16 <= num19)
            {
                if (num3 == num6)
                {
                    break;
                }

                num16 += num21;
                num3 += num9;
            }
            else
            {
                if (num4 == num7)
                {
                    break;
                }

                num19 += num22;
                num4 += num10;
            }
        }

        return null;
    }

    public List<TerrainChunk> GetVisibleChunks(Vector3 viewPosition, Vector3 viewDirection)
    {
        var vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
        var v = Vector3.Normalize(Vector3.Cross(viewDirection, vector));
        var obj = new[]
        {
            viewPosition,
            viewPosition + 6f * viewDirection,
            viewPosition + 6f * viewDirection - 6f * vector,
            viewPosition + 6f * viewDirection + 6f * vector,
            viewPosition + 6f * viewDirection - 2f * v,
            viewPosition + 6f * viewDirection + 2f * v
        };
        var list = new List<TerrainChunk>();
        foreach (var vector2 in obj)
        {
            var chunkAtCell = Terrain.GetChunkAtCell(
                Terrain.ToCell(vector2.X),
                Terrain.ToCell(vector2.Z),
                false
            );
            if (chunkAtCell is { State: TerrainChunkState.Valid } && !list.Contains(chunkAtCell))
            {
                list.Add(chunkAtCell);
            }
        }

        return list;
    }

    public void ChangeCellNet(
        int x, int y, int z,
        int value,
        bool updateModificationCounter = true,
        ComponentMiner? miner = null
    )
    {
        var cellValueFast = Terrain.GetCellValueFast(x, y, z);
        value = Terrain.ReplaceLight(value, 0);
        cellValueFast = Terrain.ReplaceLight(cellValueFast, 0);
        if (value == cellValueFast)
        {
            return;
        }

        Terrain.SetCellValueFast(x, y, z, value);
        var chunkAtCell = Terrain.GetChunkAtCell(x, z, false);
        if (chunkAtCell != null)
        {
            if (updateModificationCounter)
            {
                chunkAtCell.ModificationCounter++;
            }

            TerrainUpdater.DowngradeChunkNeighborhoodState(chunkAtCell.Coords, 1, TerrainChunkState.InvalidLight,
                false);
        }

        _modifiedCells[new Point3(x, y, z)] = true;
        var num = Terrain.ExtractContents(cellValueFast);
        var num2 = Terrain.ExtractContents(value);
        if (num2 != num)
        {
            var blockBehaviors = _subsystemBlockBehaviors.GetBlockBehaviors(num);
            foreach (var behavior in blockBehaviors)
            {
                behavior.OnBlockRemoved(cellValueFast, value, x, y, z);
            }

            var blockBehaviors2 = _subsystemBlockBehaviors.GetBlockBehaviors(num2);
            foreach (var behavior in blockBehaviors2)
            {
                if (miner == null)
                {
                    behavior.OnBlockAdded(value, cellValueFast, x, y, z);
                }
                else
                {
                    behavior.OnBlockAdded(value, cellValueFast, x, y, z);
                    behavior.OnBlockAdded(value, cellValueFast, x, y, z, miner);
                }
            }
        }
        else
        {
            var blockBehaviors3 = _subsystemBlockBehaviors.GetBlockBehaviors(num2);
            foreach (var behavior in blockBehaviors3)
            {
                behavior.OnBlockModified(value, cellValueFast, x, y, z);
            }
        }
    }

    public void ChangeCell(
        int x, int y, int z,
        int value,
        bool updateModificationCounter = true,
        ComponentMiner? miner = null
    )
    {
        var pass = false;
        ModsManager.HookAction("TerrainChangeCell", loader =>
        {
            loader.TerrainChangeCell(this, x, y, z, value, out var skip);
            pass |= skip;
            return false;
        });
        if (pass)
        {
            return;
        }

        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(x, z, out Territoriy? territoriy))
        {
            if (!territoriy!.AllowBlockBehavior)
            {
                if (miner == null ||
                    !SubsystemBedrockBlockBehavior.AllowPlayerAction(miner.ComponentPlayer, territoriy))
                {
                    miner?.ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块行为权限", Color.Yellow, false, true);
                    return;
                }
            }
            else
            {
                if (territoriy.IsVisible)
                {
                    if (SubsystemBedrockBlockBehavior.IsInTerritoriyBorder(territoriy, x, z))
                    {
                        if (miner == null ||
                            !SubsystemBedrockBlockBehavior.AllowPlayerAction(miner.ComponentPlayer, territoriy))
                        {
                            miner?.ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块行为权限", Color.Yellow, false,
                                true);
                            return;
                        }
                    }
                }
            }
        }

        ChangeCellNet(x, y, z, value, updateModificationCounter, miner);
        CommonLib.Net.QueuePackage(new SubsystemTerrainPackage(x, y, z, value));
    }

    public void DestroyCell(
        int toolLevel,
        int x, int y, int z,
        int newValue,
        bool noDrop,
        bool noParticleSystem,
        ComponentMiner? miner = null
    )
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var allowBlockBehavior = true;
        if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriy(x, z, out Territoriy? territoriy))
        {
            if (!territoriy!.AllowBlockBehavior)
            {
                allowBlockBehavior = false;
                if (miner == null ||
                    !SubsystemBedrockBlockBehavior.AllowPlayerAction(miner.ComponentPlayer, territoriy))
                {
                    miner?.ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块行为权限", Color.Yellow, false, true);
                    return;
                }
            }
            else
            {
                if (territoriy.IsVisible)
                {
                    if (SubsystemBedrockBlockBehavior.IsInTerritoriyBorder(territoriy, x, z))
                    {
                        if (miner == null ||
                            !SubsystemBedrockBlockBehavior.AllowPlayerAction(miner.ComponentPlayer, territoriy))
                        {
                            miner?.ComponentPlayer?.ComponentGui.DisplaySmallMessage("你在这里没有方块行为权限", Color.Yellow, false,
                                true);
                            return;
                        }
                    }
                }
            }
        }

        var cellValue = Terrain.GetCellValue(x, y, z);
        var num = Terrain.ExtractContents(cellValue);
        var block = BlocksManager.Blocks[num];
        if (!allowBlockBehavior && block is DoorBlock) //如果不允许方块行为且为门方块则不掉落物品，因为门为两格高的特殊方块，禁用行为后可以刷
        {
            ChangeCell(x, y, z, newValue, true, miner);
            return;
        }

        if (num != 0)
        {
            var showDebris = true;
            if (!noDrop)
            {
                _dropValues.Clear();
                block.GetDropValues(this, cellValue, newValue, toolLevel, _dropValues, out showDebris);
                foreach (var item in _dropValues)
                {
                    var dropValue = item;
                    if (dropValue.Count <= 0)
                    {
                        continue;
                    }

                    var blockBehaviors =
                        _subsystemBlockBehaviors.GetBlockBehaviors(Terrain.ExtractContents(dropValue.Value));
                    foreach (var behavior in blockBehaviors)
                    {
                        behavior.OnItemHarvested(x, y, z, cellValue, ref dropValue, ref newValue);
                    }

                    if (dropValue.Count <= 0 || Terrain.ExtractContents(dropValue.Value) == 0)
                    {
                        continue;
                    }

                    var position = new Vector3(x, y, z) + new Vector3(0.5f);
                    _subsystemPickables.AddPickable(dropValue.Value, dropValue.Count, position, null, null);
                }
            }

            if (showDebris && !noParticleSystem &&
                _subsystemViews.CalculateDistanceFromNearestView(new Vector3(x, y, z)) < 16f)
            {
                _subsystemParticles.AddParticleSystem(block.CreateDebrisParticleSystem(this,
                    new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), cellValue, 1f));
            }
        }

        ChangeCell(x, y, z, newValue, true, miner);
    }

    public void Draw(Camera camera, int drawOrder)
    {
        if (TerrainRenderingEnabled)
        {
            if (drawOrder == _drawOrders[0])
            {
                TerrainUpdater.PrepareForDrawing(camera);
                TerrainRenderer.PrepareForDrawing(camera);
                TerrainRenderer.DrawOpaque(camera);
                TerrainRenderer.DrawAlphaTested(camera);
            }
            else if (drawOrder == _drawOrders[1])
            {
                TerrainRenderer.DrawTransparent(camera);
            }
        }
    }

    public void Update(float dt)
    {
        TerrainUpdater.Update();
        ProcessModifiedCells();
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemViews = Project.FindSubsystem<SubsystemGameWidgets>(true)!;
        SubsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
        _subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true)!;
        _subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true)!;
        SubsystemAnimatedTextures = Project.FindSubsystem<SubsystemAnimatedTextures>(true)!;
        SubsystemFurnitureBlockBehavior = Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        SubsystemPalette = Project.FindSubsystem<SubsystemPalette>(true)!;
        Terrain = new Terrain();
        TerrainRenderer = new TerrainRenderer(this);
        TerrainUpdater = new TerrainUpdater(this);
        TerrainSerializer = CommonLib.WorkType != WorkType.Client
            ? new TerrainSerializer24(SubsystemGameInfo.DirectoryName)
            : new TerrainSerializerNet();
        BlockGeometryGenerator = new BlockGeometryGenerator(Terrain, this,
            Project.FindSubsystem<SubsystemElectricity>(true)!, SubsystemFurnitureBlockBehavior,
            Project.FindSubsystem<SubsystemMetersBlockBehavior>(true)!, SubsystemPalette);
        var terrainGenerationMode = SubsystemGameInfo.WorldSettings.TerrainGenerationMode;
        if (string.CompareOrdinal(SubsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.1") <= 0)
        {
            if (terrainGenerationMode is TerrainGenerationMode.FlatContinent or TerrainGenerationMode.FlatIsland)
            {
                TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
            }
            else
            {
                TerrainContentsGenerator = new TerrainContentsGenerator21(this);
            }
        }
        else if (string.CompareOrdinal(SubsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.2") == 0)
        {
            if (terrainGenerationMode is TerrainGenerationMode.FlatContinent or TerrainGenerationMode.FlatIsland)
            {
                TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
            }
            else
            {
                TerrainContentsGenerator = new TerrainContentsGenerator22(this);
            }
        }
        else if (string.CompareOrdinal(SubsystemGameInfo.WorldSettings.OriginalSerializationVersion, "2.3") == 0)
        {
            if (terrainGenerationMode is TerrainGenerationMode.FlatContinent or TerrainGenerationMode.FlatIsland)
            {
                TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
            }
            else
            {
                TerrainContentsGenerator = new TerrainContentsGenerator23(this);
            }
        }
        else if (terrainGenerationMode is TerrainGenerationMode.FlatContinent or TerrainGenerationMode.FlatIsland)
        {
            TerrainContentsGenerator = new TerrainContentsGeneratorFlat(this);
        }
        else
        {
            TerrainContentsGenerator = new TerrainContentsGenerator24(this);
        }
    }

    private void SaveChunk()
    {
        TerrainUpdater.UpdateEvent.WaitOne();
        try
        {
            var allocatedChunks = Terrain.AllocatedChunks;
            foreach (var chunk in allocatedChunks)
            {
                TerrainSerializer.SaveChunk(chunk);
            }
        }
        finally
        {
            TerrainUpdater.UpdateEvent.Set();
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        SaveChunk();
    }

    public override void Dispose()
    {
        TerrainRenderer.Dispose();
        TerrainRenderer = null!;

        TerrainUpdater.Dispose();
        TerrainUpdater = null!;

        TerrainSerializer.Dispose();
        TerrainSerializer = null!;

        Terrain.Dispose();
        Terrain = null!;

        BlockGeometryGenerator = null!;
    }
}
