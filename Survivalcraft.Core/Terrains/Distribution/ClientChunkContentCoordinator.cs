using System.Diagnostics.CodeAnalysis;

namespace Game.Terrains.Distribution;

/// <summary>权威内容安装到客户端地形的统一入口。</summary>
public sealed class ClientChunkContentCoordinator(Terrain terrain)
{
    private readonly Terrain _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));

    public event Action<TerrainChunk>? ContentInstalled;

    public bool TryInstall(ClientChunkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var target = _terrain.GetChunkAtCoords(
            snapshot.Allocation.Coords.X,
            snapshot.Allocation.Coords.Y
        );
        if (!CanInstall(target, snapshot.Allocation, snapshot.ContentVersion))
        {
            return false;
        }

        target.Cells = snapshot.Cells.ToArray();
        target.Shafts = snapshot.Shafts.ToArray();
        target.NetworkContentVersion = snapshot.ContentVersion;
        target.ContentOrigin = TerrainContentOrigin.Replica;
        target.HasContents = true;
        target.NeedsContentSnapshot = false;
        target.NeedsContentValidation = false;
        ContentInstalled?.Invoke(target);
        return true;
    }

    internal static bool CanInstall(
        [NotNullWhen(true)] TerrainChunk? target,
        ChunkAllocationId allocation,
        long contentVersion
    ) =>
        target != null &&
        target.Coords == allocation.Coords &&
        target.AllocationGeneration == allocation.Generation &&
        (contentVersion > target.NetworkContentVersion ||
         target.NeedsContentValidation && contentVersion == target.NetworkContentVersion);
}
