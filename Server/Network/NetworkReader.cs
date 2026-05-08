using System.Text;

namespace Game.Network;

public class NetworkReader : INetworkReader
{
    private readonly byte[] _data;
    private int _position;

    public NetworkReader(byte[] data)
    {
        _data = data;
        _position = 0;
    }

    public int BytesRemaining => _data.Length - _position;

    public byte ReadByte()
    {
        return _data[_position++];
    }

    public int ReadInt()
    {
        var result = _data[_position]
                     | (_data[_position + 1] << 8)
                     | (_data[_position + 2] << 16)
                     | (_data[_position + 3] << 24);
        _position += 4;
        return result;
    }

    public float ReadFloat()
    {
        var result = BitConverter.ToSingle(_data, _position);
        _position += 4;
        return result;
    }

    public string ReadString()
    {
        var length = ReadUShort();
        var result = Encoding.UTF8.GetString(_data, _position, length);
        _position += length;
        return result;
    }

    public byte[] ReadBytes(int length)
    {
        var result = new byte[length];
        Array.Copy(_data, _position, result, 0, length);
        _position += length;
        return result;
    }

    public ushort ReadUShort()
    {
        var result = (ushort)(_data[_position] | (_data[_position + 1] << 8));
        _position += 2;
        return result;
    }
}
