namespace Game.Network.NetSimulate;

public class NetRotation : NetSimulate<Quaternion>
{
    public NetRotation(Quaternion quaternion) : base(quaternion)
    {
        LerpFunc = Lerp;
    }

    public static Quaternion Lerp(Quaternion rotation, Quaternion target, float f)
    {
        return f >= 1f ? target : Quaternion.Lerp(rotation, target, f);
    }
}
