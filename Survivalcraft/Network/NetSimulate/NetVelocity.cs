namespace Game.Network.NetSimulate;

public class NetVelocity : NetSimulate<Vector3>
{
    public NetVelocity(Vector3 vector) : base(vector)
    {
        LerpFunc = (b, e, f) => f >= 1f ? e : Vector3.Lerp(b, e, f);
    }
}
