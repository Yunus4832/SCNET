using System.Globalization;
using System.Text;

using Engine.Core.Serialization;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemMovingBlocks : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private readonly List<MovingBlockSet> _movingBlockSets = [];

    public IReadOnlyList<MovingBlockSet> MovingBlockSets => _movingBlockSets;

    private int _nextNetworkId = 1;

    private uint _networkStateTick;

    private readonly DynamicArray<IMovingBlockSet> _result = [];

    private readonly List<MovingBlockSet> _stopped = [];

    private SubsystemNetworkInterest _subsystemNetworkInterest = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTime _subsystemTime = null!;

    public IReadOnlyList<IMovingBlockSet> ReadonlyMovingBlockSets => MovingBlockSets;


    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

        foreach (var movingBlockSet in MovingBlockSets)
        {
            if (!WorldRuntimeContext.For(Project).IsAuthority && movingBlockSet.NetworkStopped)
            {
                continue;
            }

            var chunkAtCell = _subsystemTerrain.Terrain.GetChunkAtCell(
                Terrain.ToCell(movingBlockSet.Position.X),
                Terrain.ToCell(movingBlockSet.Position.Z),
                false
            );
            if (chunkAtCell is not { HasContents: true })
            {
                continue;
            }

            movingBlockSet.Speed += movingBlockSet.Acceleration * _subsystemTime.GameTimeDelta;
            if (movingBlockSet.Drag != 0f)
            {
                movingBlockSet.Speed *= MathUtils.Pow(1f - movingBlockSet.Drag, _subsystemTime.GameTimeDelta);
            }

            var x = Vector3.Distance(movingBlockSet.StartPosition, movingBlockSet.Position);
            var num = Vector3.Distance(movingBlockSet.TargetPosition, movingBlockSet.Position);
            var num2 = movingBlockSet.Smoothness.X > 0f
                ? MathUtils.Saturate((MathUtils.Sqrt(x) + 0.05f) / movingBlockSet.Smoothness.X)
                : 1f;
            var num3 = movingBlockSet.Smoothness.Y > 0f
                ? MathUtils.Saturate((num + 0.05f) / movingBlockSet.Smoothness.Y)
                : 1f;
            var num4 = num2 * num3;
            var flag = false;
            var vector = num > 0f ? (movingBlockSet.TargetPosition - movingBlockSet.Position) / num : Vector3.Zero;
            var x2 = _subsystemTime.GameTimeDelta > 0f ? 0.95f / _subsystemTime.GameTimeDelta : 0f;
            var num5 = MathUtils.Min(movingBlockSet.Speed * num4, x2);
            if (num5 * _subsystemTime.GameTimeDelta >= num)
            {
                movingBlockSet.Position = movingBlockSet.TargetPosition;
                movingBlockSet.CurrentVelocity = Vector3.Zero;
                flag = true;
            }
            else
            {
                movingBlockSet.CurrentVelocity =
                    num5 / num * (movingBlockSet.TargetPosition - movingBlockSet.Position);
                movingBlockSet.Position += movingBlockSet.CurrentVelocity * _subsystemTime.GameTimeDelta;
            }

            if (!WorldRuntimeContext.For(Project).IsAuthority)
            {
                continue;
            }

            movingBlockSet.Stop = false;
            MovingBlocksCollision(movingBlockSet);
            TerrainCollision(movingBlockSet);
            if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

            if (movingBlockSet.Stop)
            {
                if (vector.X < 0f)
                {
                    movingBlockSet.Position.X = MathUtils.Ceiling(movingBlockSet.Position.X);
                }
                else if (vector.X > 0f)
                {
                    movingBlockSet.Position.X = MathUtils.Floor(movingBlockSet.Position.X);
                }

                if (vector.Y < 0f)
                {
                    movingBlockSet.Position.Y = MathUtils.Ceiling(movingBlockSet.Position.Y);
                }
                else if (vector.Y > 0f)
                {
                    movingBlockSet.Position.Y = MathUtils.Floor(movingBlockSet.Position.Y);
                }

                if (vector.Z < 0f)
                {
                    movingBlockSet.Position.Z = MathUtils.Ceiling(movingBlockSet.Position.Z);
                }
                else if (vector.Z > 0f)
                {
                    movingBlockSet.Position.Z = MathUtils.Floor(movingBlockSet.Position.Z);
                }
            }

            if (movingBlockSet.Stop | flag)
            {
                _stopped.Add(movingBlockSet);
            }
        }

        if (WorldRuntimeContext.For(Project).IsAuthority)
        {
            foreach (var item in _stopped)
            {
                SendMovingBlockEvent(item, true);
                DoStop(item);
                if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }
            }

            if (Time.PeriodicEvent(0.1, 0.0))
            {
                UpdateClientMovingBlockInterests();
            }
        }

        _stopped.Clear();
    }

    public event Action<MovingBlockSet>? Added;

    public event Action<MovingBlockSet>? Removed;

    public void AddReplicaMovingBlockSet(MovingBlockSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (!WorldRuntimeContext.IsProjectActive(Project) ||
            WorldRuntimeContext.For(Project).IsAuthority ||
            _movingBlockSets.Contains(set)) { return; }

        _movingBlockSets.Add(set);
        Added?.Invoke(set);
    }

    public override void Dispose()
    {
        Added = null;
        Removed = null;
        CollidedWithTerrain = null;
        Stopped = null;
        _movingBlockSets.Clear();
        _stopped.Clear();
        _result.Clear();
    }

    public event Action<IMovingBlockSet, Point3>? CollidedWithTerrain;

    public event Action<IMovingBlockSet>? Stopped;

    public IMovingBlockSet? AddMovingBlockSet(
        Vector3 position,
        Vector3 targetPosition,
        float speed,
        float acceleration,
        float drag,
        Vector2 smoothness,
        IEnumerable<MovingBlock> blocks,
        string id,
        object tag,
        bool testCollision
    )
    {
        if (!WorldRuntimeContext.IsProjectActive(Project)) { return null; }

        var movingBlockSet = new MovingBlockSet
        {
            NetworkId = AllocateNetworkId(),
            Position = position,
            StartPosition = position,
            TargetPosition = targetPosition,
            Speed = speed,
            Acceleration = acceleration,
            Drag = drag,
            Smoothness = smoothness,
            Id = id,
            Tag = tag
        };
        movingBlockSet.SetBlocks(blocks);
        if (testCollision)
        {
            MovingBlocksCollision(movingBlockSet);
            if (movingBlockSet.Stop)
            {
                return null;
            }
        }

        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return movingBlockSet;
        }

        _movingBlockSets.Add(movingBlockSet);
        Added?.Invoke(movingBlockSet);

        return movingBlockSet;
    }

    public void RemoveMovingBlockSet(IMovingBlockSet movingBlockSet)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        var set = (MovingBlockSet)movingBlockSet;
        SendMovingBlockEvent(set, false);
        RemoveMovingBlockSetLogic(set);
        _subsystemNetworkInterest.Entities.RemoveEntity(EntityInterestGroup.MovingBlocks, set.NetworkId);
    }

    public void RemoveMovingBlockSetLogic(IMovingBlockSet movingBlockSet)
    {
        var movingBlockSet2 = (MovingBlockSet)movingBlockSet;
        if (_movingBlockSets.Remove(movingBlockSet2) && WorldRuntimeContext.IsProjectActive(Project))
        {
            Removed?.Invoke(movingBlockSet2);
        }
    }

    public void FindMovingBlocks(BoundingBox boundingBox, bool extendToFillCells, DynamicArray<IMovingBlockSet> result)
    {
        foreach (var movingBlockSet in MovingBlockSets)
        {
            if (ExclusiveBoxIntersection(boundingBox, movingBlockSet.BoundingBox(extendToFillCells)))
            {
                result.Add(movingBlockSet);
            }
        }
    }

    public IMovingBlockSet? FindMovingBlocks(string id, object? tag)
    {
        foreach (var movingBlockSet in MovingBlockSets)
        {
            if (movingBlockSet.Id == id && Equals(movingBlockSet.Tag, tag))
            {
                return movingBlockSet;
            }
        }

        return null;
    }

    public MovingBlockSet? FindMovingBlocks(int networkId)
    {
        return _movingBlockSets.Find(movingBlockSet => movingBlockSet.NetworkId == networkId);
    }

    private int AllocateNetworkId()
    {
        return _nextNetworkId++;
    }

    private void SendMovingBlockEvent(MovingBlockSet movingBlockSet, bool stopped)
    {
        var observers = _subsystemNetworkInterest.Entities
            .GetObservers(EntityInterestGroup.MovingBlocks, movingBlockSet.NetworkId)
            .Where(client => client.IsConnected)
            .ToArray();
        if (observers.Length > 0)
        {
            NetworkSender.Send(
                Project,
                MovingBlockPackageBuilder.Capture(movingBlockSet, stopped),
                PackageAudience.To(observers)
            );
        }
    }

    private void UpdateClientMovingBlockInterests()
    {
        _networkStateTick++;
        var movingBlocks = MovingBlockSets.ToDictionary(set => set.NetworkId);
        var relevant = _subsystemNetworkInterest.GetPointCandidates(
            movingBlocks.Values.Select(set =>
                (set.NetworkId, set.Position.XZ)
            )
        );
        foreach (var client in WorldConnections.For(Project).Members.Values)
        {
            if (client == WorldConnections.For(Project).Self || !client.IsConnected)
            {
                continue;
            }

            var current = relevant.TryGetValue(client, out var ids) ? ids : [];
            var changes = _subsystemNetworkInterest.Entities.Synchronize(
                client,
                EntityInterestGroup.MovingBlocks,
                current,
                movingBlocks.ContainsKey,
                3
            );
            foreach (var networkId in changes.Entered)
            {
                if (movingBlocks.TryGetValue(networkId, out var movingBlockSet))
                {
                    NetworkSender.Send(
                        Project,
                        MovingBlockPackageBuilder.Capture(movingBlockSet) with { StateTick = _networkStateTick },
                        PackageAudience.To(client)
                    );
                }
            }

            foreach (var networkId in changes.Left)
            {
                if (movingBlocks.TryGetValue(networkId, out var movingBlockSet))
                {
                    NetworkSender.Send(
                        Project,
                        MovingBlockPackageBuilder.Capture(movingBlockSet, false),
                        PackageAudience.To(client)
                    );
                }
            }

            foreach (var networkId in current.Except(changes.Entered))
            {
                var movingBlockSet = movingBlocks[networkId];
                NetworkSender.Send(
                    Project,
                    MovingBlockPackageBuilder.CaptureState(movingBlockSet) with
                    {
                        StateTick = _networkStateTick
                    },
                    PackageAudience.To(client)
                );
            }
        }
    }

    public MovingBlocksRaycastResult? Raycast(Vector3 start, Vector3 end, bool extendToFillCells)
    {
        var ray = new Ray3(start, Vector3.Normalize(end - start));
        var boundingBox = new BoundingBox(Vector3.Min(start, end), Vector3.Max(start, end));
        _result.Clear();
        FindMovingBlocks(boundingBox, extendToFillCells, _result);
        var num = float.MaxValue;
        MovingBlockSet? movingBlockSet = null;
        foreach (var movingBlockSet1 in _result)
        {
            var item = (MovingBlockSet)movingBlockSet1;
            var box = item.BoundingBox(extendToFillCells);
            var num2 = ray.Intersection(box);
            if (!(num2 < num))
            {
                continue;
            }

            num = num2.Value;
            movingBlockSet = item;
        }

        if (movingBlockSet == null)
        {
            return null;
        }

        var value = default(MovingBlocksRaycastResult);
        value.Ray = ray;
        value.Distance = num;
        value.MovingBlockSet = movingBlockSet;
        return value;
    }

    public void DoStop(MovingBlockSet item)
    {
        Stopped?.Invoke(item);
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemNetworkInterest = Project.FindSubsystem<SubsystemNetworkInterest>(true)!;
        foreach (ValuesDictionary value9 in valuesDictionary.GetValue<ValuesDictionary>("MovingBlockSets").Values)
        {
            LoadAndAddMovingItem(value9);
        }
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        var valuesDictionary2 = new ValuesDictionary();
        valuesDictionary.SetValue("MovingBlockSets", valuesDictionary2);
        if (Project.SendToClientMode)
        {
            return;
        }

        var num = 0;
        foreach (var movingBlockSet in MovingBlockSets)
        {
            valuesDictionary2.SetValue(num++.ToString(CultureInfo.InvariantCulture), SaveMovingItem(movingBlockSet));
        }
    }

    public IMovingBlockSet? LoadAndAddMovingItem(ValuesDictionary value9)
    {
        var value = value9.GetValue<Vector3>("Position");
        var value2 = value9.GetValue<Vector3>("TargetPosition");
        var value3 = value9.GetValue<float>("Speed");
        var value4 = value9.GetValue<float>("Acceleration");
        var value5 = value9.GetValue<float>("Drag");
        var value6 = value9.GetValue("Smoothness", Vector2.Zero);
        var value7 = value9.GetValue("Id", string.Empty);
        var value8 = value9.GetValue("Tag", new object());
        var list = new List<MovingBlock>();
        var array = value9.GetValue<string>("Blocks").Split([';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var obj2 in array)
        {
            MovingBlock item = default;
            var array2 = obj2.Split([','], StringSplitOptions.RemoveEmptyEntries);
            item.Value = HumanReadableConverter.ConvertFromString<int>(array2[0]);
            item.Offset.X = HumanReadableConverter.ConvertFromString<int>(array2[1]);
            item.Offset.Y = HumanReadableConverter.ConvertFromString<int>(array2[2]);
            item.Offset.Z = HumanReadableConverter.ConvertFromString<int>(array2[3]);
            list.Add(item);
        }

        var movingBlockSet = (MovingBlockSet?)AddMovingBlockSet(
            value,
            value2,
            value3,
            value4,
            value5,
            value6,
            list,
            value7,
            value8,
            false
        );
        if (movingBlockSet is not null)
        {
            movingBlockSet.StartPosition = value9.GetValue<Vector3>("StartPosition");
            movingBlockSet.CurrentVelocity = value9.GetValue<Vector3>("CurrentVelocity");
            var networkId = value9.GetValue<int>("NetworkId");
            movingBlockSet.NetworkId = networkId;
            _nextNetworkId = Math.Max(_nextNetworkId, networkId + 1);
        }

        return movingBlockSet;
    }

    public static ValuesDictionary SaveMovingItem(MovingBlockSet movingBlockSet)
    {
        var valuesDictionary3 = new ValuesDictionary();
        valuesDictionary3.SetValue("Position", movingBlockSet.Position);
        valuesDictionary3.SetValue("StartPosition", movingBlockSet.StartPosition);
        valuesDictionary3.SetValue("CurrentVelocity", movingBlockSet.CurrentVelocity);
        valuesDictionary3.SetValue("NetworkId", movingBlockSet.NetworkId);
        valuesDictionary3.SetValue("TargetPosition", movingBlockSet.TargetPosition);
        valuesDictionary3.SetValue("Speed", movingBlockSet.Speed);
        valuesDictionary3.SetValue("Acceleration", movingBlockSet.Acceleration);
        valuesDictionary3.SetValue("Drag", movingBlockSet.Drag);
        if (movingBlockSet.Smoothness != Vector2.Zero)
        {
            valuesDictionary3.SetValue("Smoothness", movingBlockSet.Smoothness);
        }

        if (!string.IsNullOrEmpty(movingBlockSet.Id))
        {
            valuesDictionary3.SetValue("Id", movingBlockSet.Id);
        }

        if (HumanReadableConverter.IsTypeSupported(movingBlockSet.Tag.GetType()))
        {
            valuesDictionary3.SetValue("Tag", movingBlockSet.Tag);
        }

        var stringBuilder = new StringBuilder();
        foreach (var block in movingBlockSet.Blocks)
        {
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Value));
            stringBuilder.Append(',');
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Offset.X));
            stringBuilder.Append(',');
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Offset.Y));
            stringBuilder.Append(',');
            stringBuilder.Append(HumanReadableConverter.ConvertToString(block.Offset.Z));
            stringBuilder.Append(';');
        }

        valuesDictionary3.SetValue("Blocks", stringBuilder.ToString());
        return valuesDictionary3;
    }


    private void MovingBlocksCollision(MovingBlockSet movingBlockSet)
    {
        var boundingBox = movingBlockSet.BoundingBox(true);
        _result.Clear();
        FindMovingBlocks(boundingBox, true, _result);
        var num = 0;
        while (true)
        {
            if (num >= _result.Count)
            {
                return;
            }

            if (_result.Array[num] != movingBlockSet)
            {
                break;
            }

            num++;
        }

        movingBlockSet.Stop = true;
    }

    private void TerrainCollision(MovingBlockSet movingBlockSet)
    {
        var point = default(Point3);
        point.X = (int)MathUtils.Floor(movingBlockSet.Box.Left + movingBlockSet.Position.X);
        point.Y = (int)MathUtils.Floor(movingBlockSet.Box.Top + movingBlockSet.Position.Y);
        point.Z = (int)MathUtils.Floor(movingBlockSet.Box.Near + movingBlockSet.Position.Z);
        var point2 = default(Point3);
        point2.X = (int)MathUtils.Ceiling(movingBlockSet.Box.Right + movingBlockSet.Position.X);
        point2.Y = (int)MathUtils.Ceiling(movingBlockSet.Box.Bottom + movingBlockSet.Position.Y);
        point2.Z = (int)MathUtils.Ceiling(movingBlockSet.Box.Far + movingBlockSet.Position.Z);
        WorldRuleHooks.For(Project).Invoke(
            new MovingBlockSetTerrainCollisionContext(this, movingBlockSet, point, point2)
        );
        if (!WorldRuntimeContext.IsProjectActive(Project)) { return; }

        for (var i = point.X; i < point2.X; i++)
        {
            for (var j = point.Z; j < point2.Z; j++)
            {
                for (var k = point.Y; k < point2.Y; k++)
                {
                    if (Terrain.ExtractContents(_subsystemTerrain.Terrain.GetCellValue(i, k, j)) != 0)
                    {
                        CollidedWithTerrain?.Invoke(movingBlockSet, new Point3(i, k, j));
                    }
                }
            }
        }
    }


    private static bool ExclusiveBoxIntersection(BoundingBox b1, BoundingBox b2)
    {
        if (b1.Max.X > b2.Min.X &&
            b1.Min.X < b2.Max.X &&
            b1.Max.Y > b2.Min.Y &&
            b1.Min.Y < b2.Max.Y &&
            b1.Max.Z > b2.Min.Z)
        {
            return b1.Min.Z < b2.Max.Z;
        }

        return false;
    }

    public class MovingBlockSet : IMovingBlockSet
    {
        public float Acceleration;

        private readonly List<MovingBlock> _blocks = [];

        public IReadOnlyList<MovingBlock> Blocks => _blocks;

        public long ContentRevision { get; private set; }

        internal void SetBlocks(IEnumerable<MovingBlock> blocks)
        {
            _blocks.Clear();
            _blocks.AddRange(blocks);
            UpdateBox();
            ContentRevision = unchecked(ContentRevision + 1);
        }

        public Box Box;

        public Vector3 CurrentVelocity;

        public float Drag;

        public string Id = string.Empty;

        public int NetworkId;

        public uint LastNetworkStateTick;

        public bool NetworkStopped;

        public Vector3 Position;

        public Vector2 Smoothness;

        public float Speed;

        public Vector3 StartPosition;

        public bool Stop;

        public required object Tag;

        public Vector3 TargetPosition;

        Vector3 IMovingBlockSet.Position => Position;

        string IMovingBlockSet.Id => Id;

        object IMovingBlockSet.Tag => Tag;

        Vector3 IMovingBlockSet.CurrentVelocity => CurrentVelocity;

        ReadOnlyList<MovingBlock> IMovingBlockSet.Blocks => new(_blocks);

        public BoundingBox BoundingBox(bool extendToFillCells)
        {
            var min = new Vector3(Position.X + Box.Left, Position.Y + Box.Top, Position.Z + Box.Near);
            var max = new Vector3(Position.X + Box.Right, Position.Y + Box.Bottom, Position.Z + Box.Far);
            if (extendToFillCells)
            {
                min.X = MathUtils.Floor(min.X);
                min.Y = MathUtils.Floor(min.Y);
                min.Z = MathUtils.Floor(min.Z);
                max.X = MathUtils.Ceiling(max.X);
                max.Y = MathUtils.Ceiling(max.Y);
                max.Z = MathUtils.Ceiling(max.Z);
            }

            return new BoundingBox(min, max);
        }

        void IMovingBlockSet.SetBlock(Point3 offset, int value)
        {
            _blocks.RemoveAll(b => b.Offset == offset);
            if (value != 0)
            {
                _blocks.Add(
                    new MovingBlock
                    {
                        Offset = offset,
                        Value = value
                    }
                );
            }

            UpdateBox();
            ContentRevision = unchecked(ContentRevision + 1);
        }

        void IMovingBlockSet.Stop()
        {
            Stop = true;
        }

        public void UpdateBox()
        {
            Point3? point = null;
            Point3? point2 = null;
            foreach (var block in Blocks)
            {
                point = point.HasValue ? Point3.Min(point.Value, block.Offset) : block.Offset;
                point2 = point2.HasValue ? Point3.Max(point2.Value, block.Offset) : block.Offset;
            }

            if (point.HasValue)
            {
                if (point2 == null)
                {
                    return;
                }

                Box = new Box(
                    point.Value.X,
                    point.Value.Y,
                    point.Value.Z,
                    point2.Value.X - point.Value.X + 1,
                    point2.Value.Y - point.Value.Y + 1,
                    point2.Value.Z - point.Value.Z + 1
                );
            }
            else
            {
                Box = default;
            }
        }
    }
}
