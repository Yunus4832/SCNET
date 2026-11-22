using Engine.Core.Pathfinding;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Pathfinding;

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

    public void QueuePathSearch(
        Vector3 start,
        Vector3 end,
        float minDistance,
        Vector3 boxSize,
        bool ignoreDoors,
        int maxPositionsToCheck,
        PathfindingResult result
    )
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_requests)
        {
            if (!_stopRequested && _requests.Count < maxPendingRequests)
            {
                result.IsCompleted = false;
                result.IsInProgress = true;
                _requests.Enqueue(
                    new Request
                    {
                        Start = start,
                        End = end,
                        MinDistance = minDistance,
                        BoxSize = boxSize,
                        IgnoreDoors = ignoreDoors,
                        MaxPositionsToCheck = maxPositionsToCheck,
                        PathfindingResult = result
                    }
                );
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
        var isServer = WorldRuntimeContext.For(Project).IsAuthority;
        var workerCount = CalculateWorkerCount(Environment.ProcessorCount, isServer);
        for (var i = 0; i < workerCount; i++)
        {
            var workerIndex = i;
            var context = new WorkerContext(_subsystemTerrain);
            _workers.Add(
                Task.Factory.StartNew(
                    () => ThreadFunction(context, workerIndex),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default
                )
            );
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
                Log.Error($"Pathfinding worker {workerIndex} failed: {e}");
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
        for (var waypointIndex = path.Count - 2; waypointIndex > 0; waypointIndex--)
        {
            if (IsPassable(path.Array[waypointIndex + 1], path.Array[waypointIndex - 1], boxSize))
            {
                path.RemoveAt(waypointIndex);
            }
        }
    }

    public bool IsPassable(Vector3 p1, Vector3 p2, Vector3 boxSize)
    {
        var rayStart = new Vector3(p1.X, p1.Y + 0.5f, p1.Z);
        var rayEnd = new Vector3(p2.X, p2.Y + 0.5f, p2.Z);
        var sideOffset = (0.5f * boxSize.X + 0.1f) * Vector3.Normalize(Vector3.Cross(Vector3.UnitY, rayEnd - rayStart));
        if (_subsystemTerrain.Raycast(rayStart, rayEnd, false, true, SmoothingRaycastFunction_Obstacle)
            .HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(
                rayStart - sideOffset,
                rayEnd - sideOffset,
                false,
                true,
                SmoothingRaycastFunction_Obstacle
            )
            .HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(
                rayStart + sideOffset,
                rayEnd + sideOffset,
                false,
                true,
                SmoothingRaycastFunction_Obstacle
            )
            .HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(
                rayStart + new Vector3(0f, -1f, 0f),
                rayEnd + new Vector3(0f, -1f, 0f),
                false,
                false,
                SmoothingRaycastFunction_Support
            ).HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(
                rayStart + new Vector3(0f, -1f, 0f) - sideOffset,
                rayEnd + new Vector3(0f, -1f, 0f) - sideOffset,
                false,
                false,
                SmoothingRaycastFunction_Support
            ).HasValue)
        {
            return false;
        }

        if (_subsystemTerrain.Raycast(
                rayStart + new Vector3(0f, -1f, 0f) + sideOffset,
                rayEnd + new Vector3(0f, -1f, 0f) + sideOffset,
                false,
                false,
                SmoothingRaycastFunction_Support
            ).HasValue)
        {
            return false;
        }

        return true;
    }

    private bool SmoothingRaycastFunction_Obstacle(int value, float distance)
    {
        var contents = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
        if (block.ShouldAvoid(value))
        {
            return true;
        }

        return block.Collidable;
    }

    private bool SmoothingRaycastFunction_Support(int value, float distance)
    {
        var contents = Terrain.ExtractContents(value);
        var block = WorldContentBinding.For(Project).Catalog.Blocks[contents];
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
            var distanceX = MathUtils.Abs(p1.X - p2.X);
            var distanceZ = MathUtils.Abs(p1.Z - p2.Z);
            if (distanceX > distanceZ)
            {
                return 1.41f * distanceZ + 1f * (distanceX - distanceZ);
            }

            return 1.41f * distanceX + 1f * (distanceZ - distanceX);
        }

        public bool IsGoal(Vector3 p)
        {
            return Vector3.DistanceSquared(p, Request.End) <= Request.MinDistance * Request.MinDistance;
        }

        private void AddNeighbor(DynamicArray<Vector3> neighbors, Vector3 p, int dx, int dz)
        {
            var landingY = p.Y;
            var clearanceY = p.Y;
            var neighborX = Terrain.ToCell(p.X) + dx;
            var cellY = Terrain.ToCell(p.Y);
            var neighborZ = Terrain.ToCell(p.Z) + dz;
            var cellValue = SubsystemTerrain.Terrain.GetCellValue(neighborX, cellY, neighborZ);
            var neighborContents = Terrain.ExtractContents(cellValue);
            var block = SubsystemTerrain.Catalog.Blocks[neighborContents];
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

                landingY = cellY + blockWalkingHeight;
                clearanceY = cellY + blockWalkingHeight;
            }
            else
            {
                var foundSupport = false;
                for (var dropOffsetY = -1; dropOffsetY >= -4; dropOffsetY--)
                {
                    var cellValue2 = SubsystemTerrain.Terrain.GetCellValue(neighborX, cellY + dropOffsetY, neighborZ);
                    var supportContents = Terrain.ExtractContents(cellValue2);
                    var block2 = SubsystemTerrain.Catalog.Blocks[supportContents];
                    if (block2.ShouldAvoid(cellValue2))
                    {
                        return;
                    }

                    if (!block2.Collidable)
                    {
                        continue;
                    }

                    landingY = cellY + dropOffsetY + 1;
                    foundSupport = true;
                    break;
                }

                if (!foundSupport)
                {
                    return;
                }
            }

            var sampleCount = dx == 0 || dz == 0 ? 2 : 3;
            var sweepStart = new Vector3(p.X, clearanceY + 0.01f, p.Z);
            var sweepEnd = new Vector3(neighborX + 0.5f, clearanceY + 0.01f, neighborZ + 0.5f);
            var sweepStep = 1f / sampleCount * (sweepEnd - sweepStart);
            for (var i = 1; i <= sampleCount; i++)
            {
                var samplePosition = sweepStart + i * sweepStep;
                var box = new BoundingBox(
                    samplePosition - new Vector3(Request.BoxSize.X / 2f + 0.01f, 0f, Request.BoxSize.Z / 2f + 0.01f),
                    samplePosition +
                    new Vector3(
                        Request.BoxSize.X / 2f - 0.01f,
                        Request.BoxSize.Y,
                        Request.BoxSize.Z / 2f - 0.01f
                    )
                );
                if (IsBlocked(box))
                {
                    return;
                }
            }

            neighbors.Add(new Vector3(neighborX + 0.5f, landingY, neighborZ + 0.5f));
        }

        public float GetBlockWalkingHeight(Block block, int value)
        {
            if (block is DoorBlock or FenceGateBlock)
            {
                return 0f;
            }

            var walkingHeight = 0f;
            var customCollisionBoxes = block.GetCustomCollisionBoxes(SubsystemTerrain, value);
            foreach (var boundingBox in customCollisionBoxes)
            {
                walkingHeight = MathUtils.Max(walkingHeight, boundingBox.Max.Y);
            }

            return walkingHeight;
        }

        public bool IsBlocked(BoundingBox box)
        {
            var minX = Terrain.ToCell(box.Min.X);
            var minY = MathUtils.Max(Terrain.ToCell(box.Min.Y), 0);
            var minZ = Terrain.ToCell(box.Min.Z);
            var maxX = Terrain.ToCell(box.Max.X);
            var maxY = MathUtils.Min(Terrain.ToCell(box.Max.Y), 255);
            var maxZ = Terrain.ToCell(box.Max.Z);
            for (var i = minX; i <= maxX; i++)
            {
                for (var j = minZ; j <= maxZ; j++)
                {
                    var chunkAtCell = SubsystemTerrain.Terrain.GetChunkAtCell(i, j, false);
                    if (chunkAtCell == null)
                    {
                        continue;
                    }

                    var cellIndex = TerrainChunk.CalculateCellIndex(i & 0xF, minY, j & 0xF);
                    var cellY = minY;
                    while (cellY <= maxY)
                    {
                        var cellValueFast = chunkAtCell.GetCellValueFast(cellIndex);
                        var contents = Terrain.ExtractContents(cellValueFast);
                        if (contents != 0)
                        {
                            var block = SubsystemTerrain.Catalog.Blocks[contents];
                            if (block.ShouldAvoid(cellValueFast))
                            {
                                return true;
                            }

                            if (block.Collidable &&
                                (!Request.IgnoreDoors || (block is not DoorBlock && block is not TrapdoorBlock)))
                            {
                                var v = new Vector3(i, cellY, j);
                                var customCollisionBoxes =
                                    block.GetCustomCollisionBoxes(SubsystemTerrain, cellValueFast);
                                foreach (var boundingBox in customCollisionBoxes)
                                {
                                    if (box.Intersection(new BoundingBox(v + boundingBox.Min, v + boundingBox.Max)))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }

                        cellY++;
                        cellIndex++;
                    }
                }
            }

            return false;
        }
    }
}
