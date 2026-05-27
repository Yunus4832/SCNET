namespace Game.Network.NetSimulate;

public class NetPosition
{
    protected Vector3 previous;
    private float _elapsedTime;
    protected Vector3 next;

    public NetPosition(Vector3 v)
    {
        previous = v;
        next = v;
        _elapsedTime = 0;
    }

    public void SetNext(Vector3 v)
    {
        previous = next;
        next = v;
        _elapsedTime = 0;
    }

    public Vector3 Get(float dt)
    {
        _elapsedTime += dt * 8;
        if (_elapsedTime >= 1f)
        {
            return next;
        }

        return Vector3.Lerp(previous, next, _elapsedTime);
    }
}
