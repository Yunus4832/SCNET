using EntitySystem.TemplatesDatabase;
using Game.NetWork;
using Game.NetWork.Packages;

namespace Game.Subsystems;

public class SubsystemPistonBlockBehavior : SubsystemBlockBehavior, IUpdateable
{
    public const string IdString = "Piston";

    public const int PistonMaxMovedBlocks = 8;

    public const int PistonMaxExtension = 8;

    public const int PistonMaxSpeedSetting = 3;

    private readonly Dictionary<Point3, QueuedAction> _actions = new();

    private bool _allowPistonHeadRemove;

    private readonly DynamicArray<MovingBlock> _movingBlocks = [];

    private SubsystemAudio _subsystemAudio = null!;

    private SubsystemMovingBlocks _subsystemMovingBlocks = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    private readonly List<KeyValuePair<Point3, QueuedAction>> _tmpActions = [];

    public override int[] HandledBlocks => [];

    public UpdateOrder UpdateOrder => _subsystemMovingBlocks.UpdateOrder + 1;

    public void Update(float dt)
    {
        if (_subsystemTime.PeriodicGameTimeEvent(0.125, 0.0))
        {
            ProcessQueuedActions();
        }

        UpdateMovableBlocks();
    }

    public void AdjustPiston(Point3 position, int length)
    {
        if (!_actions.TryGetValue(position, out var value))
        {
            value = new QueuedAction();
            _actions[position] = value;
        }

        value.Move = length;
    }

    public override bool OnEditInventoryItem(IInventory inventory, int slotIndex, ComponentPlayer componentPlayer)
    {
        var value = inventory.GetSlotValue(slotIndex);
        inventory.GetSlotCount(slotIndex);
        var data = Terrain.ExtractData(value);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditPistonDialog(data, delegate(int newData)
        {
            var num = Terrain.ReplaceData(value, newData);
            if (num == value)
            {
                return;
            }

            var p = new EditableBlockPackage(EditableItemType.Piston, default, true, inventory.Id, slotIndex,
                newData);
            CommonLib.Net.QueuePackage(p);
            if (CommonLib.WorkType != WorkType.Client)
            {
                p.Handle(ProjectNet.Project, CommonLib.Net, false);
            }
        }));
        return true;
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer componentPlayer)
    {
        var contents = Terrain.ExtractContents(value);
        var data = Terrain.ExtractData(value);
        DialogsManager.ShowDialog(componentPlayer.GuiWidget, new EditPistonDialog(data, delegate(int newData)
        {
            if (newData == data || SubsystemTerrain.Terrain.GetCellContents(x, y, z) != contents)
            {
                return;
            }

            var cell = new CellFace(x, y, z, 0);
            var p = new EditableBlockPackage(EditableItemType.Piston, cell, false, 0, 0, newData);
            CommonLib.Net.QueuePackage(p);
            if (CommonLib.WorkType != WorkType.Client)
            {
                p.Handle(ProjectNet.Project, CommonLib.Net, false);
            }
        }));
        return true;
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        var num = Terrain.ExtractContents(value);
        var data = Terrain.ExtractData(value);
        switch (num)
        {
            case PistonBlock.Index:
            {
                StopPiston(new Point3(x, y, z));
                var face2 = PistonBlock.GetFace(data);
                var point2 = CellFace.FaceToPoint3(face2);
                var cellValue3 = _subsystemTerrain.Terrain.GetCellValue(x + point2.X, y + point2.Y, z + point2.Z);
                var num4 = Terrain.ExtractContents(cellValue3);
                var data4 = Terrain.ExtractData(cellValue3);
                if (num4 == PistonHeadBlock.Index && PistonHeadBlock.GetFace(data4) == face2)
                {
                    _subsystemTerrain.DestroyCell(0, x + point2.X, y + point2.Y, z + point2.Z, 0, false, false);
                }

                break;
            }
            case PistonHeadBlock.Index:
                if (!_allowPistonHeadRemove)
                {
                    var face = PistonHeadBlock.GetFace(data);
                    var point = CellFace.FaceToPoint3(face);
                    var cellValue = _subsystemTerrain.Terrain.GetCellValue(x + point.X, y + point.Y, z + point.Z);
                    var cellValue2 = _subsystemTerrain.Terrain.GetCellValue(x - point.X, y - point.Y, z - point.Z);
                    var num2 = Terrain.ExtractContents(cellValue);
                    var num3 = Terrain.ExtractContents(cellValue2);
                    var data2 = Terrain.ExtractData(cellValue);
                    var data3 = Terrain.ExtractData(cellValue2);
                    if (num2 == PistonHeadBlock.Index && PistonHeadBlock.GetFace(data2) == face)
                    {
                        _subsystemTerrain.DestroyCell(0, x + point.X, y + point.Y, z + point.Z, 0, false, false);
                    }

                    if (num3 == PistonBlock.Index && PistonBlock.GetFace(data3) == face ||
                        num3 == PistonHeadBlock.Index && PistonHeadBlock.GetFace(data3) == face)
                    {
                        _subsystemTerrain.DestroyCell(0, x - point.X, y - point.Y, z - point.Z, 0, false, false);
                    }
                }

                break;
        }
    }

    public override void OnChunkDiscarding(TerrainChunk chunk)
    {
        var boundingBox = new BoundingBox(chunk.BoundingBox.Min - new Vector3(16f),
            chunk.BoundingBox.Max + new Vector3(16f));
        var dynamicArray = new DynamicArray<IMovingBlockSet>();
        _subsystemMovingBlocks.FindMovingBlocks(boundingBox, false, dynamicArray);
        foreach (var item in dynamicArray)
        {
            if (item.Id == IdString)
            {
                StopPiston((Point3)item.Tag);
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _subsystemMovingBlocks = Project.FindSubsystem<SubsystemMovingBlocks>(true)!;
        _subsystemMovingBlocks.Stopped += MovingBlocksStopped;
        _subsystemMovingBlocks.CollidedWithTerrain += MovingBlocksCollidedWithTerrain;
    }

    public void ProcessQueuedActions()
    {
        _tmpActions.Clear();
        _tmpActions.AddRange(_actions);
        foreach (var tmpAction in _tmpActions)
        {
            var key = tmpAction.Key;
            var value = tmpAction.Value;
            if (Terrain.ExtractContents(_subsystemTerrain.Terrain.GetCellValue(key.X, key.Y, key.Z)) != 237)
            {
                StopPiston(key);
                value.Move = null;
                value.Stop = false;
            }
            else if (value.Stop)
            {
                StopPiston(key);
                value.Stop = false;
                value.StoppedFrame = Time.FrameIndex;
            }
        }

        foreach (var (key2, value2) in _tmpActions)
        {
            if (value2 is not { Move: not null, Stop: false } || Time.FrameIndex == value2.StoppedFrame ||
                _subsystemMovingBlocks.FindMovingBlocks(IdString, key2) != null)
            {
                continue;
            }

            var flag = true;
            for (var i = -1; i <= 1; i++)
            for (var j = -1; j <= 1; j++)
            {
                var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(key2.X + i * 16, key2.Z + j * 16, false);
                if (chunkAtCell is not { State: > TerrainChunkState.InvalidContents4 })
                {
                    flag = false;
                }
            }

            if (flag && MovePiston(key2, value2.Move.Value))
            {
                value2.Move = null;
            }
        }

        foreach (var (key3, value3) in _tmpActions)
        {
            if (!value3.Move.HasValue && !value3.Stop)
            {
                _actions.Remove(key3);
            }
        }
    }

    public void UpdateMovableBlocks()
    {
        foreach (var movingBlockSet in _subsystemMovingBlocks.ReadonlyMovingBlockSets)
        {
            if (movingBlockSet.Id == IdString)
            {
                var point = (Point3)movingBlockSet.Tag;
                var cellValue = _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
                if (Terrain.ExtractContents(cellValue) != 237)
                {
                    continue;
                }

                var data = Terrain.ExtractData(cellValue);
                var mode = PistonBlock.GetMode(data);
                var face = PistonBlock.GetFace(data);
                var p = CellFace.FaceToPoint3(face);
                var num = int.MaxValue;
                foreach (var block in movingBlockSet.Blocks)
                {
                    num = MathUtils.Min(num, block.Offset.X * p.X + block.Offset.Y * p.Y + block.Offset.Z * p.Z);
                }

                var num2 = movingBlockSet.Position.X * p.X + movingBlockSet.Position.Y * p.Y +
                           movingBlockSet.Position.Z * p.Z;
                float num3 = point.X * p.X + point.Y * p.Y + point.Z * p.Z;
                if (num2 > num3)
                {
                    if (num + num2 - num3 > 1f)
                    {
                        movingBlockSet.SetBlock(p * (num - 1),
                            Terrain.MakeBlockValue(238, 0,
                                PistonHeadBlock.SetFace(
                                    PistonHeadBlock.SetIsShaft(PistonHeadBlock.SetMode(0, mode), true), face)));
                    }
                }
                else if (num2 < num3 && num + num2 - num3 <= 0f)
                {
                    movingBlockSet.SetBlock(p * num, 0);
                }
            }
        }
    }

    public static void GetSpeedAndSmoothness(int pistonSpeed, out float speed, out Vector2 smoothness)
    {
        switch (pistonSpeed)
        {
            default:
                speed = 5f;
                smoothness = new Vector2(0f, 0.5f);
                break;
            case 1:
                speed = 4.5f;
                smoothness = new Vector2(0.6f, 0.6f);
                break;
            case 2:
                speed = 4f;
                smoothness = new Vector2(0.9f, 0.9f);
                break;
            case 3:
                speed = 3.5f;
                smoothness = new Vector2(1.2f, 1.2f);
                break;
        }
    }

    public bool MovePiston(Point3 position, int length)
    {
        if (CommonLib.WorkType == WorkType.Client)
        {
            return true;
        }

        return MovePistonNet(position, length);
    }

    public bool MovePistonNet(Point3 position, int length)
    {
        var terrain = _subsystemTerrain.Terrain;
        var data = Terrain.ExtractData(terrain.GetCellValue(position.X, position.Y, position.Z));
        var face = PistonBlock.GetFace(data);
        var mode = PistonBlock.GetMode(data);
        var maxExtension = PistonBlock.GetMaxExtension(data);
        var pullCount = PistonBlock.GetPullCount(data);
        var speed = PistonBlock.GetSpeed(data);
        var point = CellFace.FaceToPoint3(face);
        length = MathUtils.Clamp(length, 0, maxExtension + 1);
        var num = 0;
        _movingBlocks.Clear();
        var offset = point;
        MovingBlock item;
        while (_movingBlocks.Count < 8)
        {
            var cellValue = terrain.GetCellValue(position.X + offset.X, position.Y + offset.Y, position.Z + offset.Z);
            var num2 = Terrain.ExtractContents(cellValue);
            var face2 = PistonHeadBlock.GetFace(Terrain.ExtractData(cellValue));
            if (num2 != PistonHeadBlock.Index || face2 != face)
            {
                break;
            }

            var movingBlocks = _movingBlocks;
            item = new MovingBlock
            {
                Offset = offset,
                Value = cellValue
            };
            movingBlocks.Add(item);
            offset += point;
            num++;
        }

        if (length > num)
        {
            var movingBlocks2 = _movingBlocks;
            item = new MovingBlock
            {
                Offset = Point3.Zero,
                Value = Terrain.MakeBlockValue(PistonHeadBlock.Index, 0,
                    PistonHeadBlock.SetFace(PistonHeadBlock.SetMode(PistonHeadBlock.SetIsShaft(0, num > 0), mode),
                        face))
            };
            movingBlocks2.Add(item);
            var num3 = 0;
            var pass = true;
            while (num3 < 8)
            {
                if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriyBorder(position.X + offset.X,
                        position.Z + offset.Z, out var territoriy))
                {
                    if (territoriy!.IsVisible)
                    {
                        pass = false;
                        break;
                    }
                }

                var cellValue2 =
                    terrain.GetCellValue(position.X + offset.X, position.Y + offset.Y, position.Z + offset.Z);
                if (!IsBlockMovable(cellValue2, face, position.Y + offset.Y, out var isEnd))
                {
                    break;
                }

                var movingBlocks3 = _movingBlocks;
                item = new MovingBlock
                {
                    Offset = offset,
                    Value = cellValue2
                };
                movingBlocks3.Add(item);
                num3++;
                offset += point;
                if (isEnd)
                {
                    break;
                }
            }

            if (!IsBlockBlocking(terrain.GetCellValue(position.X + offset.X, position.Y + offset.Y,
                    position.Z + offset.Z)) && pass)
            {
                GetSpeedAndSmoothness(speed, out var speed2, out var smoothness);
                var p = position + (length - num) * point;
                if (_subsystemMovingBlocks.AddMovingBlockSet(new Vector3(position) + 0.01f * new Vector3(point),
                        new Vector3(p), speed2, 0f, 0f, smoothness, _movingBlocks, IdString, position, true) != null)
                {
                    _allowPistonHeadRemove = true;
                    try
                    {
                        foreach (var movingBlock in _movingBlocks)
                        {
                            if (movingBlock.Offset != Point3.Zero)
                            {
                                _subsystemTerrain.ChangeCell(position.X + movingBlock.Offset.X,
                                    position.Y + movingBlock.Offset.Y, position.Z + movingBlock.Offset.Z, 0);
                            }
                        }
                    }
                    finally
                    {
                        _allowPistonHeadRemove = false;
                    }

                    _subsystemTerrain.ChangeCell(position.X, position.Y, position.Z,
                        Terrain.MakeBlockValue(PistonBlock.Index, 0, PistonBlock.SetIsExtended(data, true)));
                    _subsystemAudio.PlaySound("Audio/Piston", 1f, 0f, new Vector3(position), 2f, true);
                }
            }

            return false;
        }

        if (length < num)
        {
            if (mode != 0)
            {
                var num4 = 0;
                for (var i = 0; i < pullCount + 1; i++)
                {
                    if (SubsystemBedrockBlockBehavior.CheckIsInTerritoriyBorder(position.X + offset.X,
                            position.Z + offset.Z, out var territoriy))
                    {
                        if (territoriy!.IsVisible)
                        {
                            break;
                        }
                    }

                    var cellValue3 = terrain.GetCellValue(position.X + offset.X, position.Y + offset.Y,
                        position.Z + offset.Z);
                    if (!IsBlockMovable(cellValue3, face, position.Y + offset.Y, out var isEnd2))
                    {
                        break;
                    }

                    var movingBlocks4 = _movingBlocks;
                    item = new MovingBlock
                    {
                        Offset = offset,
                        Value = cellValue3
                    };
                    movingBlocks4.Add(item);
                    offset += point;
                    num4++;
                    if (isEnd2)
                    {
                        break;
                    }
                }

                if (mode == PistonMode.StrictPulling && num4 < pullCount + 1)
                {
                    return false;
                }
            }

            GetSpeedAndSmoothness(speed, out var speed3, out var smoothness2);
            var s = length == 0 ? 0.01f : 0f;
            var targetPosition = new Vector3(position) + (length - num) * new Vector3(point) + s * new Vector3(point);
            if (_subsystemMovingBlocks.AddMovingBlockSet(new Vector3(position), targetPosition, speed3, 0f, 0f,
                    smoothness2, _movingBlocks, IdString, position, true) != null)
            {
                _allowPistonHeadRemove = true;
                try
                {
                    foreach (var movingBlock2 in _movingBlocks)
                    {
                        _subsystemTerrain.ChangeCell(position.X + movingBlock2.Offset.X,
                            position.Y + movingBlock2.Offset.Y, position.Z + movingBlock2.Offset.Z, 0);
                    }
                }
                finally
                {
                    _allowPistonHeadRemove = false;
                }

                _subsystemAudio.PlaySound("Audio/Piston", 1f, 0f, new Vector3(position), 2f, true);
            }

            return false;
        }

        return true;
    }

    public void StopPiston(Point3 position)
    {
        if (CommonLib.WorkType != WorkType.Client)
        {
            StopPistonNet(position);
        }
    }

    public void StopPistonNet(Point3 position)
    {
        var movingBlockSet = _subsystemMovingBlocks.FindMovingBlocks(IdString, position);
        if (movingBlockSet != null)
        {
            var cellValue = _subsystemTerrain.Terrain.GetCellValue(position.X, position.Y, position.Z);
            var num = Terrain.ExtractContents(cellValue);
            var data = Terrain.ExtractData(cellValue);
            var flag = num == PistonBlock.Index;
            var isExtended = false;
            _subsystemMovingBlocks.RemoveMovingBlockSet(movingBlockSet);
            foreach (var block in movingBlockSet.Blocks)
            {
                var x = Terrain.ToCell(MathUtils.Round(movingBlockSet.Position.X)) + block.Offset.X;
                var y = Terrain.ToCell(MathUtils.Round(movingBlockSet.Position.Y)) + block.Offset.Y;
                var z = Terrain.ToCell(MathUtils.Round(movingBlockSet.Position.Z)) + block.Offset.Z;
                if (!(new Point3(x, y, z) == position))
                {
                    var num2 = Terrain.ExtractContents(block.Value);
                    if (flag || num2 != PistonHeadBlock.Index)
                    {
                        _subsystemTerrain.DestroyCell(0, x, y, z, block.Value, false, false);
                        if (num2 == PistonHeadBlock.Index)
                        {
                            isExtended = true;
                        }
                    }
                }
            }

            if (flag)
            {
                _subsystemTerrain.ChangeCell(position.X, position.Y, position.Z,
                    Terrain.MakeBlockValue(PistonBlock.Index, 0, PistonBlock.SetIsExtended(data, isExtended)));
            }
        }
    }

    public void MovingBlocksCollidedWithTerrain(IMovingBlockSet movingBlockSet, Point3 p)
    {
        if (movingBlockSet.Id != IdString)
        {
            return;
        }

        var point = (Point3)movingBlockSet.Tag;
        var cellValue = _subsystemTerrain.Terrain.GetCellValue(point.X, point.Y, point.Z);
        if (Terrain.ExtractContents(cellValue) != PistonBlock.Index)
        {
            return;
        }

        var point2 = CellFace.FaceToPoint3(PistonBlock.GetFace(Terrain.ExtractData(cellValue)));
        var num = p.X * point2.X + p.Y * point2.Y + p.Z * point2.Z;
        var num2 = point.X * point2.X + point.Y * point2.Y + point.Z * point2.Z;
        if (num <= num2)
        {
            return;
        }

        if (IsBlockBlocking(SubsystemTerrain.Terrain.GetCellValue(p.X, p.Y, p.Z)))
        {
            movingBlockSet.Stop();
        }
        else
        {
            SubsystemTerrain.DestroyCell(0, p.X, p.Y, p.Z, 0, false, false);
        }
    }

    public void MovingBlocksStopped(IMovingBlockSet movingBlockSet)
    {
        if (movingBlockSet.Id != IdString || movingBlockSet.Tag is not Point3 key)
        {
            return;
        }

        if (Terrain.ExtractContents(_subsystemTerrain.Terrain.GetCellValue(key.X, key.Y, key.Z)) == PistonBlock.Index)
        {
            if (!_actions.TryGetValue(key, out var value))
            {
                value = new QueuedAction();
                _actions.Add(key, value);
            }

            value.Stop = true;
        }
    }

    public static bool IsBlockMovable(int value, int pistonFace, int y, out bool isEnd)
    {
        isEnd = false;
        var num = Terrain.ExtractContents(value);
        var data = Terrain.ExtractData(value);
        switch (num)
        {
            case CraftingTableBlock.Index:
            case ChestBlock.Index:
            case FurnaceBlock.Index:
            case LitFurnaceBlock.Index:
            case DispenserBlock.Index:
                return false;
            case FurnitureBlock.Index:
                return true;
            case PistonBlock.Index:
                return !PistonBlock.GetIsExtended(data);
            case PistonHeadBlock.Index:
            case PumpkinBlock.Index:
            case JackOLanternBlock.Index:
            case RottenPumpkinBlock.Index:
            case CactusBlock.Index:
            case DiamondBlock.Index:
                return false;
            case BedrockBlock.Index:
                if (data == 1)
                {
                    return false;
                }

                return y > 1;
            default:
            {
                var block = BlocksManager.Blocks[num];
                if (block is BottomSuckerBlock)
                {
                    return false;
                }

                if (block is MountedElectricElementBlock elementBlock)
                {
                    isEnd = true;
                    return elementBlock.GetFace(value) == pistonFace;
                }

                if (block is DoorBlock or TrapdoorBlock)
                {
                    return false;
                }

                if (block is LadderBlock)
                {
                    isEnd = true;
                    return pistonFace == LadderBlock.GetFace(data);
                }

                if (block is not AttachedSignBlock)
                {
                    return block is { NonDuplicable: false, Collidable: true };
                }

                isEnd = true;
                return pistonFace == AttachedSignBlock.GetFace(data);
            }
        }
    }

    public static bool IsBlockBlocking(int value)
    {
        var num = Terrain.ExtractContents(value);
        return BlocksManager.Blocks[num].Collidable;
    }

    public class QueuedAction
    {
        public int? Move;

        public bool Stop;
        public int StoppedFrame;
    }
}
