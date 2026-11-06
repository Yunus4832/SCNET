using EntitySystem.Core;

namespace Game.Modding;

public sealed class ExplosionPointProcessingContext(
    SubsystemExplosions explosions,
    Point3 point,
    CharacterData? player
) : IWorldHookContext
{
    public Project Project => Explosions.Project;

    public SubsystemExplosions Explosions { get; } = explosions;

    public Point3 Point { get; } = point;

    public CharacterData? Character { get; } = player;

    public bool Cancel { get; set; }
}
