namespace Game.Network;

public interface INetworkWriter
{
    void Write(byte value);
    void Write(int value);
    void Write(float value);
    void Write(string value);
    void Write(byte[] data);
    int Position { get; }
    byte[] ToArray();
}
