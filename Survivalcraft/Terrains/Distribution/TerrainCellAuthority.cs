namespace Game.Terrains.Distribution;

/// <summary>
/// Owns authoritative single-cell reads and writes independently from any render replica.
/// </summary>
public sealed class TerrainCellAuthority(Terrain terrain)
{
    public Terrain Terrain { get; } = terrain ?? throw new ArgumentNullException(nameof(terrain));

    public int GetCellValue(int x, int y, int z) => Terrain.GetCellValue(x, y, z);

    public bool ChangeCell(int x, int y, int z, int value, bool updateModificationCounter = true)
    {
        if (!Terrain.IsCellValid(x, y, z))
        {
            return false;
        }

        var chunk = Terrain.GetChunkAtCell(x, z, false);
        if (chunk == null)
        {
            return false;
        }

        value = Terrain.ReplaceLight(value, 0);
        var oldValue = Terrain.ReplaceLight(chunk.GetCellValueFast(x & 15, y, z & 15), 0);
        if (oldValue == value)
        {
            return false;
        }

        chunk.SetCellValueFast(x & 15, y, z & 15, value);
        if (updateModificationCounter)
        {
            chunk.ModificationCounter++;
        }

        return true;
    }
}
