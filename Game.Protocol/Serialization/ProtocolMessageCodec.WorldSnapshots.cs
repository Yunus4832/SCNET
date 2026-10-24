using Game.Protocol.Blocks;
using Game.Protocol.Characters;
using Game.Protocol.Connection;
using Game.Protocol.Entities;
using Game.Protocol.Teams;
using Game.Protocol.World;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static BootstrapPackage ReadBootstrap(ProtocolReader reader)
    {
        var epoch = reader.ReadGuid();
        var clients = ReadClient(reader);
        var count = reader.ReadUInt16();
        if (count > ProtocolLimits.BlockCapacity)
        {
            throw new InvalidDataException("Too many block mappings.");
        }

        var entries = new BlockIndexEntry[count];
        var ids = new HashSet<ResourceId>();
        var indices = new HashSet<ushort>();
        for (var i = 0; i < count; i++)
        {
            var id = reader.ReadString();
            if (id.Length > ProtocolLimits.MaximumResourceIdLength)
            {
                throw new InvalidDataException("Block resource ID is too long.");
            }

            ResourceId resource;
            try { resource = ResourceId.Parse(id); }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Invalid block resource ID.", exception);
            }

            var index = reader.ReadUInt16();
            if (index >= ProtocolLimits.BlockCapacity || !ids.Add(resource) || !indices.Add(index))
            {
                throw new InvalidDataException("Invalid or duplicate block mapping.");
            }

            entries[i] = new BlockIndexEntry(resource, index);
        }

        return new BootstrapPackage
        {
            Epoch = epoch,
            ClientList = clients,
            BlockMapping = entries,
            World = ReadWorld(reader),
            TextureData = reader.ReadBuff(),
            ProjectData = reader.ReadBuff()
        };
    }

    private static void WriteBootstrap(BootstrapPackage message, ProtocolWriter writer)
    {
        if (message.BlockMapping.Count > ProtocolLimits.BlockCapacity)
        {
            throw new ArgumentException("Too many block mappings.");
        }

        writer.Write(message.Epoch);
        WriteClient(message.ClientList, writer);
        writer.Write((ushort)message.BlockMapping.Count);
        foreach (var entry in message.BlockMapping.OrderBy(item => item.Id.ToString(), StringComparer.Ordinal))
        {
            if (entry.Id.ToString().Length > ProtocolLimits.MaximumResourceIdLength ||
                entry.Index >= ProtocolLimits.BlockCapacity)
            {
                throw new ArgumentException("Invalid block mapping.");
            }

            writer.Write(entry.Id.ToString());
            writer.Write(entry.Index);
        }

        WriteWorld(message.World, writer);
        writer.WriteBuff(message.TextureData);
        writer.WriteBuff(message.ProjectData);
    }

    private static WorldMetadata ReadWorld(ProtocolReader reader)
    {
        var name = reader.ReadString();
        var seed = reader.ReadInt32();
        var mode = reader.ReadByte();
        var maximumPlayers = reader.ReadUInt16();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256)
        {
            throw new InvalidDataException("Invalid bootstrap world metadata.");
        }

        return new WorldMetadata(name, seed, mode, maximumPlayers);
    }

    private static void WriteWorld(WorldMetadata world, ProtocolWriter writer)
    {
        if (string.IsNullOrWhiteSpace(world.Name) || world.Name.Length > 256)
        {
            throw new ArgumentException("Invalid bootstrap world metadata.");
        }

        writer.Write(world.Name);
        writer.Write(world.Seed);
        writer.Write(world.Mode);
        writer.Write(world.MaximumPlayers);
    }

    private static InitialWorldSnapshotPackage ReadInitialSnapshot(ProtocolReader reader)
    {
        var epoch = reader.ReadGuid();
        var clients = ReadClient(reader);
        var count = reader.ReadUInt16();
        EnsureRemaining(reader, count * sizeof(int));
        var players = new byte[count][];
        for (var i = 0; i < count; i++) { players[i] = reader.ReadBuff(); }

        var summaries = ReadCharacterEntities(reader);
        var entities = reader.ReadBuff();
        return new InitialWorldSnapshotPackage
        {
            Epoch = epoch,
            ClientList = clients,
            Characters = players,
            EntityData = entities,
            CharacterEntities = summaries
        };
    }

    private static void WriteInitialSnapshot(InitialWorldSnapshotPackage message, ProtocolWriter writer)
    {
        if (message.Characters.Count > ushort.MaxValue) { throw new ArgumentException("Too many player snapshots."); }

        writer.Write(message.Epoch);
        WriteClient(message.ClientList, writer);
        writer.Write((ushort)message.Characters.Count);
        foreach (var player in message.Characters) { writer.WriteBuff(player); }

        WriteCharacterEntities(message.CharacterEntities, writer);
        writer.WriteBuff(message.EntityData);
    }

    private static EntityPackage ReadEntity(ProtocolReader reader)
    {
        var type = reader.ReadEnum<EntityPackage.EventType>();
        switch (type)
        {
            case EntityPackage.EventType.LoadOne:
            case EntityPackage.EventType.LoadList:
                var count = reader.ReadCount(ushort.MaxValue);
                EnsureRemaining(reader, count * (sizeof(int) + 1));
                var players = new CharacterSnapshot[count];
                for (var i = 0; i < count; i++)
                {
                    var client = reader.ReadBoolean() ? ReadClient(reader).Client : null;
                    players[i] = new CharacterSnapshot(reader.ReadBuff(), client);
                }

                var summaries = ReadCharacterEntities(reader);
                var data = reader.ReadBuff();
                return new EntityPackage
                {
                    Type = type,
                    Characters = players,
                    EntityData = data,
                    CharacterEntities = summaries
                };
            case EntityPackage.EventType.Remove:
                return new EntityPackage(reader.ReadInt32());
            case EntityPackage.EventType.RequestSync:
                var idsCount = reader.ReadUInt16();
                EnsureRemaining(reader, idsCount * sizeof(int));
                var ids = new int[idsCount];
                for (var i = 0; i < ids.Length; i++) { ids[i] = reader.ReadInt32(); }

                return new EntityPackage(ids);
            default:
                throw new InvalidDataException("Unknown entity message mode.");
        }
    }

    private static void WriteEntity(EntityPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case EntityPackage.EventType.LoadOne:
            case EntityPackage.EventType.LoadList:
                if (message.Characters.Count > ushort.MaxValue)
                {
                    throw new ArgumentException("Too many entity player snapshots.");
                }

                writer.Write(message.Characters.Count);
                foreach (var player in message.Characters)
                {
                    writer.Write(player.Client != null);
                    if (player.Client != null)
                    {
                        writer.WriteEnum(ClientPackage.EventType.Add);
                        WriteClientIdentity(player.Client, writer);
                    }

                    writer.WriteBuff(player.Data);
                }

                WriteCharacterEntities(message.CharacterEntities, writer);
                writer.WriteBuff(message.EntityData);
                break;
            case EntityPackage.EventType.Remove:
                writer.Write(message.EntityId);
                break;
            case EntityPackage.EventType.RequestSync:
                if (message.EntityIdList.Count > ushort.MaxValue)
                {
                    throw new ArgumentException("Too many requested entity IDs.");
                }

                writer.Write((ushort)message.EntityIdList.Count);
                foreach (var id in message.EntityIdList) { writer.Write(id); }

                break;
            default:
                throw new ArgumentException("Unknown entity message mode.");
        }
    }

    private static CharacterEntitySnapshot[] ReadCharacterEntities(ProtocolReader reader)
    {
        var count = reader.ReadUInt16();
        if (count > ProtocolLimits.MaximumCharacterEntitySnapshots)
        {
            throw new InvalidDataException("Too many player entity summaries.");
        }

        EnsureRemaining(reader, count * 37);
        var values = new CharacterEntitySnapshot[count];
        var identities = new HashSet<Guid>();
        var entities = new HashSet<int>();
        for (var i = 0; i < count; i++)
        {
            var value = new CharacterEntitySnapshot(
                reader.ReadGuid(),
                reader.ReadInt32(),
                reader.ReadVector3(),
                reader.ReadInt32()
            );
            if (value.CharacterId == Guid.Empty ||
                value.EntityId <= 0 ||
                value.ParentBodyId < 0 ||
                !identities.Add(value.CharacterId) ||
                !entities.Add(value.EntityId) ||
                !float.IsFinite(value.Position.X) ||
                !float.IsFinite(value.Position.Y) ||
                !float.IsFinite(value.Position.Z))
            {
                throw new InvalidDataException("Invalid player entity summary.");
            }

            values[i] = value;
        }

        return values;
    }

    private static void WriteCharacterEntities(IReadOnlyList<CharacterEntitySnapshot> values, ProtocolWriter writer)
    {
        if (values.Count > ProtocolLimits.MaximumCharacterEntitySnapshots)
        {
            throw new ArgumentException("Too many player entity summaries.");
        }

        writer.Write((ushort)values.Count);
        foreach (var value in values)
        {
            writer.Write(value.CharacterId);
            writer.Write(value.EntityId);
            writer.Write(value.Position);
            writer.Write(value.ParentBodyId);
        }
    }

    private static void EnsureRemaining(ProtocolReader reader, int minimumBytes)
    {
        if (minimumBytes > reader.BaseStream.Length - reader.BaseStream.Position)
        {
            throw new InvalidDataException("Truncated snapshot collection.");
        }
    }
}
