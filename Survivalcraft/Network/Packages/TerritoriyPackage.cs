using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class TerritoriyPackage : IPackage
{
    public bool AllowDig;
    public bool AllowPlace;
    public bool ApplyToFriend;

    public Guid Guid;
    public bool IsVisible;

    public TerritoriyPackage()
    {
    }

    public TerritoriyPackage(Territoriy territoriy)
    {
        Guid = territoriy.OwnerGuid;
        AllowDig = territoriy.AllowDig;
        AllowPlace = territoriy.AllowPlace;
        ApplyToFriend = territoriy.ApplyToFriend;
        IsVisible = territoriy.IsVisible;
    }

    public byte ID => (byte)PackageType.Territoriy;
    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Guid);
        byte flag = 0;
        if (AllowDig)
        {
            flag |= 1;
        }

        if (AllowPlace)
        {
            flag |= 2;
        }

        if (ApplyToFriend)
        {
            flag |= 4;
        }

        if (IsVisible)
        {
            flag |= 8;
        }

        writer.Write(flag);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Guid = reader.ReadGuid();
        var flag = reader.ReadByte();
        AllowDig = (flag & 1) != 0;
        AllowPlace = (flag & 2) != 0;
        ApplyToFriend = (flag & 4) != 0;
        IsVisible = (flag & 8) != 0;
    }
}
