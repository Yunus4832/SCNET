using System.Text;

namespace Game.Network;

public class NetworkWriter : INetworkWriter
{
    private readonly List<byte> _buffer = [];

    public int Position => _buffer.Count;

    public void Write(byte value)
    {
        _buffer.Add(value);
    }

    public void Write(int value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
        _buffer.Add((byte)((value >> 16) & 0xFF));
        _buffer.Add((byte)((value >> 24) & 0xFF));
    }

    public void Write(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        _buffer.AddRange(bytes);
    }

    public void Write(string value)
    {
        Write((ushort)value.Length);
        var bytes = Encoding.UTF8.GetBytes(value);
        _buffer.AddRange(bytes);
    }

    public void Write(byte[] data)
    {
        _buffer.AddRange(data);
    }

    public void Write(ushort value)
    {
        _buffer.Add((byte)(value & 0xFF));
        _buffer.Add((byte)((value >> 8) & 0xFF));
    }

    public byte[] ToArray()
    {
        return _buffer.ToArray();
    }

    public void Clear()
    {
        _buffer.Clear();
    }
}
