namespace Game.Network.Snapshots;

public static class ClothingPackageBuilder
{
    public static ComponentClothingPackage Capture(string skinName, ComponentClothingPackage.DataType type)
    {
        var data = Array.Empty<byte>();
        if (type is ComponentClothingPackage.DataType.ReplySkin or ComponentClothingPackage.DataType.WhoHasReply &&
            CharacterSkinsManager.HasSkinRes(skinName))
        {
            using var stream = Storage.OpenFile(
                Storage.CombinePaths(GamePaths.CharacterSkins, skinName),
                OpenFileMode.Read
            );
            data = StreamUtils.ReadBytes(stream);
        }

        return type switch
        {
            ComponentClothingPackage.DataType.RequestSkin => new SkinRequestPackage { SkinName = skinName },
            ComponentClothingPackage.DataType.ReplySkin => new SkinReplyPackage
                { SkinName = skinName, SkinData = data },
            ComponentClothingPackage.DataType.WhoHas => new SkinAvailabilityRequestPackage
                { SkinName = skinName },
            ComponentClothingPackage.DataType.WhoHasReply => new SkinAvailabilityReplyPackage
                { SkinName = skinName, SkinData = data },
            _ => throw new InvalidOperationException("Unsupported message operation.")
        };
    }
}
