namespace Game.Network;

public interface INetworkReader
{
    byte ReadByte();
    int ReadInt();
    float ReadFloat();
    string ReadString();
    byte[] ReadBytes(int length);
    int BytesRemaining { get; }
}
