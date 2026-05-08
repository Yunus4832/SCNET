namespace Game.NetWork;

//说明 简单防止内存修改作弊
public class SafeFloat
{
    private readonly Random _random = new();

    private int _seed;

    private int _value;

    public float Get()
    {
        var i = _value ^ _seed;
        var data = BitConverter.GetBytes(i);
        return BitConverter.ToSingle(data, 0);
    }

    public void Set(float v)
    {
        _seed = _random.Int();
        var data = BitConverter.GetBytes(v);
        var i = BitConverter.ToInt32(data, 0);
        i ^= _seed;
        _value = i;
    }
}
