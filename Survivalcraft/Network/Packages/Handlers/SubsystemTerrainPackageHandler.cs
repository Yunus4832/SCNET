using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemTerrainPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        switch (Type)
        {
            case DataType.RequestSyncChunks:
                if(From is null)
                {
                    break;
                }

                if (!subsystemTerrain.TerrainUpdater.WaitChunkList.TryGetValue(From, out var list))
                {
                    list = [];
                    subsystemTerrain.TerrainUpdater.WaitChunkList.Add(From, list);
                }

                list.AddRange(RelateChunks);
                break;
            case DataType.SyncTerrainChunkList:
                foreach (var c in Chunks)
                {
                    ApplyOneChunk(subsystemTerrain, c);
                }

                break;
            case DataType.RequestChangeCell:
            case DataType.ChangeCell:
            {
                var chunkX = X >> 4;
                var chunkZ = Z >> 4;
                var chunk = subsystemTerrain.Terrain.GetChunkAtCoords(chunkX, chunkZ);
                if (chunk != null)
                {
                    if (Type == DataType.RequestChangeCell)
                    {
                        subsystemTerrain.ChangeCell(X, Y, Z, Value);
                    }
                    else
                    {
                        subsystemTerrain.ChangeCellNet(X, Y, Z, Value);
                    }
                }
            }
                break;
            case DataType.ReplyResult:
                foreach (var p in RelateChunks)
                {
                    var chunk2 = subsystemTerrain.Terrain.GetChunkAtCoords(p.X, p.Y);
                    if (chunk2 == null)
                    {
                        continue;
                    }

                    chunk2.IsRequested = false;
                    chunk2.WasUpgraded = true;
                    chunk2.WasDowngraded = true;
                }

                break;
            case DataType.ChangeCellList:
                foreach (var cellChange in CellChanges)
                {
                    var chunkX = cellChange.X >> 4;
                    var chunkZ = cellChange.Y >> 4;
                    var chunk = subsystemTerrain.Terrain.GetChunkAtCoords(chunkX, chunkZ);
                    if (chunk != null)
                    {
                        subsystemTerrain.ChangeCellNet(cellChange.X, cellChange.Y, cellChange.Z, cellChange.Value);
                    }
                }

                break;
        }
    }
}

public sealed class SubsystemTerrainPackageHandler : PackageHandlerBase<SubsystemTerrainPackage>
{
    public override void Handle(SubsystemTerrainPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemTerrainPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
