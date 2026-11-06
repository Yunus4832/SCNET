namespace Game.Terrains;

/// <summary>Limits horizontal body sweeps to collision contents, independently of lighting and meshes.</summary>
internal static class TerrainMovementBoundary
{
    internal static bool HasCollisionContents(TerrainChunk? chunk)
    {
        return chunk is { MainThreadState: >= TerrainChunkState.InvalidLight };
    }

    internal static bool HasCollisionContents(Terrain terrain, Vector3 position)
    {
        return HasCollisionContents(
            terrain.GetChunkAtCell(Terrain.ToCell(position.X), Terrain.ToCell(position.Z), false)
        );
    }

    internal static float ClipAxisMove(Terrain terrain, Vector3 position, Vector3 size, int axis, float move)
    {
        if (axis == 1 || move == 0f) { return move; }

        var positive = move > 0f;
        var center = axis == 0 ? position.X : position.Z;
        var halfSize = (axis == 0 ? size.X : size.Z) / 2f;
        var crossCenter = axis == 0 ? position.Z : position.X;
        var crossHalfSize = (axis == 0 ? size.Z : size.X) / 2f;
        var face = center + (positive ? halfSize : -halfSize);
        var first = Terrain.ToCell(positive ? MathF.BitDecrement(face) : MathF.BitIncrement(face)) >> 4;
        var lastFace = face + move;
        var last = Terrain.ToCell(positive ? MathF.BitDecrement(lastFace) : MathF.BitIncrement(lastFace)) >> 4;
        var crossMin = Terrain.ToCell(MathF.BitIncrement(crossCenter - crossHalfSize)) >> 4;
        var crossMax = Terrain.ToCell(MathF.BitDecrement(crossCenter + crossHalfSize)) >> 4;
        var centerCoordinate = Terrain.ToCell(center) >> 4;
        var centerCross = Terrain.ToCell(crossCenter) >> 4;
        var step = positive ? 1 : -1;
        for (var coordinate = first; positive ? coordinate <= last : coordinate >= last; coordinate += step)
        {
            for (var cross = crossMin; cross <= crossMax; cross++)
            {
                if (HasCollisionContents(GetChunk(terrain, axis, coordinate, cross))) { continue; }

                // An existing overlap across the other axis must not prevent parallel escape.
                if (cross != centerCross && !HasCollisionContents(GetChunk(terrain, axis, centerCoordinate, cross)))
                {
                    continue;
                }

                var boundary = (positive ? coordinate : coordinate + 1) * 16f;
                return positive
                    ? Math.Clamp(boundary - face, 0f, move)
                    : -Math.Clamp(face - boundary, 0f, -move);
            }
        }

        return move;
    }

    private static TerrainChunk? GetChunk(Terrain terrain, int axis, int coordinate, int cross)
    {
        return axis == 0
            ? terrain.GetChunkAtCoords(coordinate, cross)
            : terrain.GetChunkAtCoords(cross, coordinate);
    }
}
