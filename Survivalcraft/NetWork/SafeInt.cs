namespace Game.NetWork;

//说明 简单防止内存修改作弊
public class SafeInt
{
    private readonly Random _random;
    private int _seed;
    private int _value;

    public SafeInt()
    {
        _random = new Random();
    }

    public int Get()
    {
        var i = _value ^ _seed;
        var data = BitConverter.GetBytes(i);
        return BitConverter.ToInt32(data, 0);
    }

    public void Set(int v)
    {
        _seed = _random.Int();
        var data = BitConverter.GetBytes(v);
        var i = BitConverter.ToInt32(data, 0);
        i ^= _seed;
        _value = i;
    }
}
