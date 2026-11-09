namespace Game.Network.Chunks;

internal readonly record struct ChunkDeliveryView(Vector2 Center, Vector2 PredictedCenter, float ContentDistance)
{
    internal bool Contains(Point2 coords) =>
        NetworkTerrainPolicy.IsChunkCenterRelevant(coords, Center, ContentDistance);

    internal float Priority(Point2 coords)
    {
        var center = new Vector2((coords.X + 0.5f) * 16f, (coords.Y + 0.5f) * 16f);
        return Vector2.DistanceSquared(PredictedCenter, center) * 0.7f +
               Vector2.DistanceSquared(Center, center) * 0.3f;
    }
}
