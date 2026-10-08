namespace Game.Modding;

public sealed class ExplosionPointProcessingContext(
    SubsystemExplosions explosions,
    Point3 point,
    PlayerData? player)
{
    public SubsystemExplosions Explosions { get; } = explosions;
    public Point3 Point { get; } = point;
    public PlayerData? Player { get; } = player;
    public bool Cancel { get; set; }
}
