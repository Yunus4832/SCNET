using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class TerritoriyPackage : IPackage
{
    public bool AllowBehavior;
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
        AllowBehavior = territoriy.AllowBlockBehavior;
        AllowDig = territoriy.AllowDig;
        AllowPlace = territoriy.AllowPlace;
        ApplyToFriend = territoriy.ApplyToFriend;
        IsVisible = territoriy.IsVisible;
    }

    public byte ID => (byte)PackageType.Territoriy;
    public Client? To { get; set; }
    public Client? Except { get; set; }
    public Client? From { get; set; }
    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Guid);
        byte flag = 0;
        if (AllowBehavior)
        {
            flag |= 1;
        }

        if (AllowDig)
        {
            flag |= 2;
        }

        if (AllowPlace)
        {
            flag |= 4;
        }

        if (ApplyToFriend)
        {
            flag |= 8;
        }

        if (IsVisible)
        {
            flag |= 16;
        }

        writer.Write(flag);
    }

    public void ReadData(PackageStreamReader reader)
    {
        Guid = reader.ReadGuid();
        var flag = reader.ReadByte();
        if ((flag & 1) == 1)
        {
            AllowBehavior = true;
        }

        if (((flag >> 1) & 1) == 1)
        {
            AllowDig = true;
        }

        if (((flag >> 2) & 1) == 1)
        {
            AllowPlace = true;
        }

        if (((flag >> 3) & 1) == 1)
        {
            ApplyToFriend = true;
        }

        if (((flag >> 4) & 1) == 1)
        {
            IsVisible = true;
        }
    }


}
