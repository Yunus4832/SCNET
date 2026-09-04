using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;

namespace Game.Subsystems;

public class SubsystemPathfinding : Subsystem
{
    internal const int maxPendingRequests = 10;

    internal const int maxServerWorkers = 4;

    internal const int maxClientWorkers = 2;

    private readonly Queue<Request> _requests = new();

    private readonly List<Task> _workers = [];

    private SubsystemTerrain _subsystemTerrain = null!;

    private bool _stopRequested;

    internal int PendingRequestCount
    {
        get
        {
            lock (_requests)
            {
                return _requests.Count;
            }
        }
    }

    internal int WorkerCount => _workers.Count;

    internal static int CalculateWorkerCount(int processorCount, bool isServer)
    {
        var maximum = isServer ? maxServerWorkers : maxClientWorkers;
        return Math.Clamp((Math.Max(processorCount, 1) + 3) / 4, 1, maximum);
    }

    public void QueuePathSearch(Vector3 start, Vector3 end, float minDistance, Vector3 boxSize, bool ignoreDoors,
        int maxPositionsToCheck, PathfindingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_requests)
        {
            if (!_stopRequested && _requests.Count < maxPendingRequests)
            {
                result.IsCompleted = false;
                result.IsInProgress = true;
                _requests.Enqueue(new Request
                {
                    Start = start,
                    End = end,
                    MinDistance = minDistance,
                    BoxSize = boxSize,
                    IgnoreDoors = ignoreDoors,
                    MaxPositionsToCheck = maxPositionsToCheck,
                    PathfindingResult = result
                });
                Monitor.Pulse(_requests);
            }
            else
            {
                result.IsCompleted = true;
                result.IsInProgress = false;
                result.Path.Clear();
                result.PathCost = 0f;
                result.PositionsChecked = 0;
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _stopRequested = false;
        var isServer = CommonLib.WorkType == WorkType.Server;
        var workerCount = CalculateWorkerCount(Environment.ProcessorCount, isServer);
        for (var i = 0; i < workerCount; i++)
        {
            var workerIndex = i;
            var context = new WorkerContext(_subsystemTerrain);
            _workers.Add(Task.Factory.StartNew(
                () => ThreadFunction(context, workerIndex),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default));
        }
    }

    public override void Dispose()
    {
        List<Request> abandoned;
        lock (_requests)
        {
            _stopRequested = true;
            abandoned = new List<Request>(_requests);
            _requests.Clear();
            Monitor.PulseAll(_requests);
        }

        foreach (var request in abandoned)
        {
            CompleteFailedRequest(request.PathfindingResult);
        }

        if (_workers.Count <= 0)
        {
            return;
        }

        Task.WaitAll(_workers.ToArray());
        _workers.Clear();
    }

    private void ThreadFunction(WorkerContext context, int workerIndex)
    {
        while (true)
        {
            Request request;
            lock (_requests)
            {
                while (_requests.Count == 0 && !_stopRequested)
                {
                    Monitor.Wait(_requests);
                }

                if (_stopRequested && _requests.Count == 0)
                {
                    return;
                }

                request = _requests.Dequeue();
            }

            try
            {
                ProcessRequest(request, context);
            }
            catch (Exception e)
            {
                CompleteFailedRequest(request.PathfindingResult);
                Log.Error(ExceptionManager.MakeFullErrorMessage(
                    $"Pathfinding worker {workerIndex} failed.", e));
            }
        }
    }

    public void ProcessRequest(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProcessRequest(request, new WorkerContext(_subsystemTerrain));
    }

    private void ProcessRequest(Request request, WorkerContext context)
    {
        var world = (World)context.AStar.World!;
        world.Request = request;
        context.AStar.Path = request.PathfindingResult.Path;
        context.AStar.FindPath(request.Start, request.End, request.MinDistance, request.MaxPositionsToCheck);
        SmoothPath(context.AStar.Path, request.BoxSize);
        request.PathfindingResult.PathCost = context.AStar.PathCost;
        request.PathfindingResult.PositionsChecked = ((Storage)context.AStar.ClosedStorage).Dictionary.Count;
        request.PathfindingResult.IsInProgress = false;
        request.PathfindingResult.IsCompleted = true;
    }

    private static void CompleteFailedRequest(PathfindingResult result)
    {
        result.Path.Clear();
        result.PathCost = 0f;
        result.PositionsChecked = 0;
        result.IsInProgress = false;
        result.IsCompleted = true;
    }

    public void SmoothPath(DynamicArray<Vector3> path, Vector3 boxSize)
    {
        for (var num = path.Count - 2; num > 0; num--)
        {
            if (IsPassable(path.Array[num + 1], path.Array[num - 1], boxSize))
            {
                path.RemoveAt(num);
            }
        }
    }

    public bool IsPassable(Vector3 p1, Vector3 p2, Vector3 boxSize)
    {
        var vector = new Vector3(p1.X, p1.Y + 0.5f, p1.Z);
        var vector2 = new Vector3(p2.X, p2.Y + 0.5f, p2.Z);
        var v = (0.5f * boxSize.X + 0.1f) * Vector3.Normalize(Vector3.Cross(Vector3.UnitY, vector2 - vector));
        if (_subsystemTerrain.Raycast(vector, vector2, false, true, SmoothingRaycastFunction_Obstacle)
            .HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(vector - v, vector2 - v, false, true, SmoothingRaycastFunction_Obstacle)
            .HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(vector + v, vector2 + v, false, true, SmoothingRaycastFunction_Obstacle)
            .HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(vector + new Vector3(0f, -1f, 0f), vector2 + new Vector3(0f, -1f, 0f), false,
                false, SmoothingRaycastFunction_Support).HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(vector + new Vector3(0f, -1f, 0f) - v, vector2 + new Vector3(0f, -1f, 0f) - v,
                false, false, SmoothingRaycastFunction_Support).HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(vector + new Vector3(0f, -1f, 0f) + v, vector2 + new Vector3(0f, -1f, 0f) + v,
                false, false, SmoothingRaycastFunction_Support).HasValue)
        {
            return false;
        }

        return true;
    }

    public static bool SmoothingRaycastFunction_Obstacle(int value, float distance)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block.ShouldAvoid(value))
        {
            return true;
        }

        return block.Collidable;
    }

    public static bool SmoothingRaycastFunction_Support(int value, float distance)
    {
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block.ShouldAvoid(value))
        {
            return true;
        }

        return !block.Collidable;
    }

    public class Request
    {
        public Vector3 BoxSize;

        public required Vector3 Start;

        public required Vector3 End;

        public required bool IgnoreDoors;

        public required int MaxPositionsToCheck;

        public required float MinDistance;

        public required PathfindingResult PathfindingResult;
    }

    private sealed class WorkerContext(SubsystemTerrain subsystemTerrain)
    {
        public AStar<Vector3> AStar { get; } = new()
        {
            OpenStorage = new Storage(),
            ClosedStorage = new Storage(),
            World = new World
            {
                SubsystemTerrain = subsystemTerrain
            }
        };
    }

    private class Storage : IAStarStorage<Vector3>
    {
        public readonly Dictionary<Vector3, object?> Dictionary = new();

        public void Clear()
        {
            Dictionary.Clear();
        }

        public object? Get(Vector3 p)
        {
            Dictionary.TryGetValue(p, out var value);
            return value;
        }

        public void Set(Vector3 p, object? data)
        {
            Dictionary[p] = data;
        }
    }

    public class World : IAStarWorld<Vector3>
    {
        public Request Request = null!;

        public required SubsystemTerrain SubsystemTerrain;

        public float Cost(Vector3 p1, Vector3 p2)
        {
            return 0.999f - 0.1f * Vector3.Dot(Vector3.Normalize(p2 - p1), Vector3.Normalize(Request.End - p1));
        }

        public void Neighbors(Vector3 p, DynamicArray<Vector3> neighbors)
        {
            neighbors.Count = 0;
            AddNeighbor(neighbors, p, 1, 0);
            AddNeighbor(neighbors, p, -1, 0);
            AddNeighbor(neighbors, p, 0, -1);
            AddNeighbor(neighbors, p, 0, 1);
            AddNeighbor(neighbors, p, -1, -1);
            AddNeighbor(neighbors, p, 1, -1);
            AddNeighbor(neighbors, p, 1, 1);
            AddNeighbor(neighbors, p, -1, 1);
        }

        public float Heuristic(Vector3 p1, Vector3 p2)
        {
            var num = MathUtils.Abs(p1.X - p2.X);
            var num2 = MathUtils.Abs(p1.Z - p2.Z);
            if (num > num2)
            {
                return 1.41f * num2 + 1f * (num - num2);
            }

            return 1.41f * num + 1f * (num2 - num);
        }

        public bool IsGoal(Vector3 p)
        {
            return Vector3.DistanceSquared(p, Request.End) <= Request.MinDistance * Request.MinDistance;
        }

        private void AddNeighbor(DynamicArray<Vector3> neighbors, Vector3 p, int dx, int dz)
        {
            var y = p.Y;
            var num = p.Y;
            var num2 = Terrain.ToCell(p.X) + dx;
            var num3 = Terrain.ToCell(p.Y);
            var num4 = Terrain.ToCell(p.Z) + dz;
            var cellValue = SubsystemTerrain.Terrain.GetCellValue(num2, num3, num4);
            var num5 = Terrain.ExtractContents(cellValue);
            var block = BlocksManager.Blocks[num5];
            if (block.ShouldAvoid(cellValue))
            {
                return;
            }

            if (block.Collidable)
            {
                var blockWalkingHeight = GetBlockWalkingHeight(block, cellValue);
                if (blockWalkingHeight > 0.5f && (block.NoAutoJump || block.NoSmoothRise))
                {
                    return;
                }

                y = num3 + blockWalkingHeight;
                num = num3 + blockWalkingHeight;
            }
            else
            {
                var flag = false;
                for (var num6 = -1; num6 >= -4; num6--)
                {
                    var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(num2, num3 + num6, num4);
                    var num7 = Terrain.ExtractContents(cellValue2);
                    var block2 = BlocksManager.Blocks[num7];
                    if (block2.ShouldAvoid(cellValue2))
                    {
                        return;
                    }

                    if (!block2.Collidable)
                    {
                        continue;
                    }

                    y = num3 + num6 + 1;
                    flag = true;
                    break;
                }

                if (!flag)
                {
                    return;
                }
            }

            var num8 = dx == 0 || dz == 0 ? 2 : 3;
            var vector = new Vector3(p.X, num + 0.01f, p.Z);
            var v = new Vector3(num2 + 0.5f, num + 0.01f, num4 + 0.5f);
            var v2 = 1f / num8 * (v - vector);
            for (var i = 1; i <= num8; i++)
            {
                var v3 = vector + i * v2;
                var box = new BoundingBox(
                    v3 - new Vector3(Request.BoxSize.X / 2f + 0.01f, 0f, Request.BoxSize.Z / 2f + 0.01f),
                    v3 + new Vector3(Request.BoxSize.X / 2f - 0.01f, Request.BoxSize.Y,
                        Request.BoxSize.Z / 2f - 0.01f));
                if (IsBlocked(box))
                {
                    return;
                }
            }

            neighbors.Add(new Vector3(num2 + 0.5f, y, num4 + 0.5f));
        }

        public float GetBlockWalkingHeight(Block block, int value)
        {
            if (block is DoorBlock or FenceGateBlock)
            {
                return 0f;
            }

            var num = 0f;
            var customCollisionBoxes = block.GetCustomCollisionBoxes(SubsystemTerrain, value);
            foreach (var boundingBox in customCollisionBoxes)
            {
                num = MathUtils.Max(num, boundingBox.Max.Y);
            }

            return num;
        }

        public bool IsBlocked(BoundingBox box)
        {
            var num = Terrain.ToCell(box.Min.X);
            var num2 = MathUtils.Max(Terrain.ToCell(box.Min.Y), 0);
            var num3 = Terrain.ToCell(box.Min.Z);
            var num4 = Terrain.ToCell(box.Max.X);
            var num5 = MathUtils.Min(Terrain.ToCell(box.Max.Y), 255);
            var num6 = Terrain.ToCell(box.Max.Z);
            for (var i = num; i <= num4; i++)
            {
                for (var j = num3; j <= num6; j++)
                {
                    var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(i, j, false);
                    if (chunkAtCell == null)
                    {
                        continue;
                    }

                    var num7 = TerrainChunk.CalculateCellIndex(i & 0xF, num2, j & 0xF);
                    var num8 = num2;
                    while (num8 <= num5)
                    {
                        var cellValueFast = chunkAtCell.GetCellValueFast(num7);
                        var num9 = Terrain.ExtractContents(cellValueFast);
                        if (num9 != 0)
                        {
                            var block = BlocksManager.Blocks[num9];
                            if (block.ShouldAvoid(cellValueFast))
                            {
                                return true;
                            }

                            if (block.Collidable &&
                                (!Request.IgnoreDoors || (block is not DoorBlock && block is not TrapdoorBlock)))
                            {
                                var v = new Vector3(i, num8, j);
                                var customCollisionBoxes = block.GetCustomCollisionBoxes(SubsystemTerrain, cellValueFast);
                                foreach (var boundingBox in customCollisionBoxes)
                                {
                                    if (box.Intersection(new BoundingBox(v + boundingBox.Min, v + boundingBox.Max)))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        num8++;
                        num7++;
                    }
                }
            }

            return false;
        }
    }
}
