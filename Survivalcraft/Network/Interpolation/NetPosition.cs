namespace Game.Network.Interpolation;

public class NetPosition(Vector3 v)
{
    protected Vector3 previous = v;

    private float _elapsedTime = 0;

    protected Vector3 next = v;

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
