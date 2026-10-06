using Game.Network;
using Game.Network.Packages;
using Game.Network.Serialization;

using LiteNetLib.Utils;

namespace ServerLoadTool;

/// <summary>Reuse current in-repository codecs without initializing game state or package handlers.</summary>
public static class LoadWire
{
    private static readonly Dictionary<byte, Type> _types = typeof(BootstrapPackage).Assembly.GetTypes()
        .Where(type => !type.IsAbstract && typeof(IPackage).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null)
        .ToDictionary(type => ((IPackage)Activator.CreateInstance(type)!).ID);

    public static NetDataWriter Encode(IPackage package, byte? playerId = null)
    {
        using var writer = new PackageStreamWriter();
        writer.Write((byte)0x88);
        writer.Write(package.ID);
        if (package is ComponentPlayerPackage)
        {
            writer.Write(playerId ?? throw new ArgumentException("Player action requires the connection player ID."));
        }

        package.WriteData(writer);
        return CommonLib.GetWriter(writer, out _);
    }

    public static IReadOnlyList<IPackage> Decode(NetDataReader data)
    {
        using var reader = CommonLib.GetReader(data);
        var packages = new List<IPackage>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            if (reader.ReadByte() != 0x88)
            {
                throw new InvalidDataException("Invalid package marker.");
            }

            var id = reader.ReadByte();
            if (!_types.TryGetValue(id, out var type))
            {
                throw new InvalidDataException($"Unknown package ID: {id}.");
            }

            var package = (IPackage)Activator.CreateInstance(type)!;
            package.ReadData(reader);
            packages.Add(package);
        }

        return packages;
    }
}
