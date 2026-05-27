namespace Game.Network.NetSimulate;

public abstract class NetSimulate<T>(T v)
{
    protected T previous = v;

    private float _elapsedTime;

    public Func<T, T, float, T> LerpFunc = null!;

    protected T next = v;

    public virtual void SetNext(T v)
    {
        previous = next;
        next = v;
        _elapsedTime = 0;
    }

    public virtual T Get(float dt)
    {
        _elapsedTime += dt * 8;
        return LerpFunc.Invoke(previous, next, _elapsedTime);
    }
}
