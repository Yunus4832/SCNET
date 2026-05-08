namespace Game;

public class Projectile : WorldItem
{
    public Vector3 AngularVelocity;

    public bool IsFireProjectile;

    public bool IsIncendiary;

    public bool IsInWater;

    public double LastNoiseTime;

    public bool NoChunk;

    public ComponentCreature? Owner;

    public ProjectileStoppedAction ProjectileStoppedAction;

    public Vector3 Rotation;

    public Vector3 TrailOffset;

    public ITrailParticleSystem? TrailParticleSystem;
}
