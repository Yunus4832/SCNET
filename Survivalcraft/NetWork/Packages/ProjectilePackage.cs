namespace Game.NetWork.Packages;

public class ProjectilePackage : IPackage
{
    public enum ProjectileTailInfo : byte
    {
        None = 0,
        Smoke = 1,
        Fireworks = 2,
        IsOffsetZero = 4
    }

    private Vector3 _angularVelocity;

    private bool _isFireProjectile;

    private ushort _ownerId;

    private Vector3 _position;

    private Vector3 _trailOffset;

    private int _value;

    private Vector3 _velocity;

    public byte ID => (byte)PackageType.Projectile;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public ProjectilePackage()
    {
    }

    public ProjectilePackage(Projectile projectile)
    {
        _value = projectile.Value;
        _position = projectile.Position;
        _velocity = projectile.Velocity;
        _trailOffset = projectile.TrailOffset;
        _angularVelocity = projectile.AngularVelocity;
        _ownerId = projectile.Owner == null ? (ushort)0 : projectile.Owner.Entity.EntityId;
        _isFireProjectile = projectile.IsFireProjectile;
    }


    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_value);
        writer.Write(_position);
        writer.Write(_velocity);
        writer.Write(_trailOffset);
        writer.Write(_angularVelocity);
        writer.Write(_ownerId);
        writer.Write(_isFireProjectile);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _value = reader.ReadInt32();
        _position = reader.ReadVector3();
        _velocity = reader.ReadVector3();
        _trailOffset = reader.ReadVector3();
        _angularVelocity = reader.ReadVector3();
        _ownerId = reader.ReadUInt16();
        _isFireProjectile = reader.ReadBoolean();
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        var subsystem = projectNet.FindSubsystem<SubsystemProjectiles>(true)!;
        ComponentCreature? creature = null;
        if (_ownerId != 0)
        {
            projectNet.FindEntityById(_ownerId, entity => { creature = entity.FindComponent<ComponentCreature>(); });
        }

        var proj = _isFireProjectile
            ? subsystem.FireProjectileNet(_value, _position, _velocity, _angularVelocity, creature)
            : subsystem.AddProjectileNet(_value, _position, _velocity, _angularVelocity, creature);
        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }
}
