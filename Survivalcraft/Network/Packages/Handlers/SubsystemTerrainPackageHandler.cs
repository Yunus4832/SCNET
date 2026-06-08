namespace Game.Network.Packages.Handlers;

public sealed class SubsystemTerrainPackageHandler : PackageHandlerBase<SubsystemTerrainPackage>
{
    public override void Handle(SubsystemTerrainPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        switch (package.Type)
        {
            case SubsystemTerrainPackage.DataType.RequestSyncChunks:
                if (package.From is null)
                {
                    break;
                }

                if (!subsystemTerrain.TerrainUpdater.WaitChunkList.TryGetValue(package.From, out var list))
                {
                    list = [];
                    subsystemTerrain.TerrainUpdater.WaitChunkList.Add(package.From, list);
                }

                list.AddRange(package.RelateChunks);
                break;
            case SubsystemTerrainPackage.DataType.SyncTerrainChunkList:
                foreach (var c in package.Chunks)
                {
                    package.ApplyOneChunk(subsystemTerrain, c);
                }

                break;
            case SubsystemTerrainPackage.DataType.RequestChangeCell:
            case SubsystemTerrainPackage.DataType.ChangeCell:
            {
                var chunkX = package.X >> 4;
                var chunkZ = package.Z >> 4;
                var chunk = subsystemTerrain.Terrain.GetChunkAtCoords(chunkX, chunkZ);
                if (chunk != null)
                {
                    if (package.Type == SubsystemTerrainPackage.DataType.RequestChangeCell)
                    {
                        subsystemTerrain.ChangeCell(package.X, package.Y, package.Z, package.Value);
                    }
                    else
                    {
                        subsystemTerrain.ChangeCellNet(package.X, package.Y, package.Z, package.Value);
                    }
                }
            }
                break;
            case SubsystemTerrainPackage.DataType.ReplyResult:
                foreach (var p in package.RelateChunks)
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
            case SubsystemTerrainPackage.DataType.ChangeCellList:
                foreach (var cellChange in package.CellChanges)
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
