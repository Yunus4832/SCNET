namespace Game.ParticleSystems;

public interface ITrailParticleSystem
{
    Vector3 Position { get; set; }

    bool IsStopped { get; set; }
}
