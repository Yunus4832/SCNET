using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public struct CellChange
{
    public int X;
    public int Y;
    public int Z;
    public int Value;
}

public class SubsystemTerrainPackage : IPackage
{
    public enum DataType
    {
        RequestSyncChunks,
        SyncTerrainChunkList,
        ReplyResult,
        ChangeCell,
        RequestChangeCell,
        ChangeCellList // 有这个的话，后面创世神，命令方块处理方块就不用一个包一个包的发了
    }

    private readonly List<CellChange> _cellChanges = [];

    public int[] Cells = [];

    public List<TerrainChunk> Chunks = [];

    private List<Point3> _modifyCells = [];

    private List<int> _modifyValues = [];

    private DataType _type;

    private int _value;

    private int _x;

    private int _y;

    private int _z;

    public List<Point2> RelateChunks = [];

    public int[] Shafts = [];

    public byte ID => (byte)PackageType.SubsystemTerrain;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public SubsystemTerrainPackage()
    {
    }

    public SubsystemTerrainPackage(List<Point2> points)
    {
        _type = DataType.RequestSyncChunks;
        RelateChunks.AddRange(points);
    }

    public SubsystemTerrainPackage(List<Point2> resultPoints, byte r)
    {
        _type = DataType.ReplyResult;
        RelateChunks.AddRange(resultPoints);
    }

    public SubsystemTerrainPackage(List<TerrainChunk> chunks)
    {
        _type = DataType.SyncTerrainChunkList;
        Chunks.AddRange(chunks);
    }

    public SubsystemTerrainPackage(List<CellChange> changeList)
    {
        _cellChanges = changeList;
        _type = DataType.ChangeCellList;
    }

    public SubsystemTerrainPackage(int x, int y, int z, int v, bool request = false)
    {
        _type = request ? DataType.RequestChangeCell : DataType.ChangeCell;
        _x = x;
        _y = y;
        _z = z;
        _value = v;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write((byte)_type);
        switch (_type)
        {
            case DataType.RequestSyncChunks:
                writer.Write(RelateChunks.Count);
                foreach (var p in RelateChunks)
                {
                    writer.Write(p);
                }

                break;
            case DataType.SyncTerrainChunkList:
                writer.Write((ushort)Chunks.Count);
                foreach (var c in Chunks)
                {
                    WriteOneChunk(writer, c);
                }

                break;
            case DataType.RequestChangeCell:
            case DataType.ChangeCell:
                writer.Write(_x);
                writer.Write(_y);
                writer.Write(_z);
                writer.Write(_value);
                break;
            case DataType.ReplyResult:
                writer.Write((ushort)RelateChunks.Count);
                foreach (var p in RelateChunks)
                {
                    writer.Write(p);
                }

                break;
            case DataType.ChangeCellList:
                writer.Write(_cellChanges.Count);
                foreach (var cellChange in _cellChanges)
                {
                    writer.Write(cellChange.X);
                    writer.Write(cellChange.Y);
                    writer.Write(cellChange.Z);
                    writer.Write(cellChange.Value);
                }

                break;
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _type = (DataType)reader.ReadByte();
        switch (_type)
        {
            case DataType.RequestSyncChunks:
                RelateChunks = new List<Point2>();
                var count = reader.ReadInt32();
                while (count-- > 0)
                {
                    RelateChunks.Add(reader.ReadPoint2());
                }

                break;
            case DataType.SyncTerrainChunkList:
                var cn = reader.ReadUInt16();
                Chunks = new List<TerrainChunk>(cn);
                while (cn-- > 0)
                {
                    ReadChunks(reader);
                }

                break;
            case DataType.RequestChangeCell:
            case DataType.ChangeCell:
                _x = reader.ReadInt32();
                _y = reader.ReadInt32();
                _z = reader.ReadInt32();
                _value = reader.ReadInt32();
                break;
            case DataType.ReplyResult:
                RelateChunks = new List<Point2>();
                var mc = reader.ReadUInt16();
                while (mc-- > 0)
                {
                    RelateChunks.Add(reader.ReadPoint2());
                }

                break;
            case DataType.ChangeCellList:
                var cellCount = reader.ReadInt32();
                while (cellCount-- > 0)
                {
                    var cellChange = new CellChange();
                    cellChange.X = reader.ReadInt32();
                    cellChange.Y = reader.ReadInt32();
                    cellChange.Z = reader.ReadInt32();
                    cellChange.Value = reader.ReadInt32();
                    _cellChanges.Add(cellChange);
                }

                break;
        }
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystemTerrain = project.FindSubsystem<SubsystemTerrain>(true)!;
        switch (_type)
        {
            case DataType.RequestSyncChunks:
                if(From is null)
                {
                    break;
                }

                if (!subsystemTerrain.TerrainUpdater.WaitChunkList.TryGetValue(From, out var list))
                {
                    list = [];
                    subsystemTerrain.TerrainUpdater.WaitChunkList.Add(From, list);
                }

                list.AddRange(RelateChunks);
                break;
            case DataType.SyncTerrainChunkList:
                foreach (var c in Chunks)
                {
                    ApplyOneChunk(subsystemTerrain, c);
                }

                break;
            case DataType.RequestChangeCell:
            case DataType.ChangeCell:
            {
                var chunkX = _x >> 4;
                var chunkZ = _z >> 4;
                var chunk = subsystemTerrain.Terrain.GetChunkAtCoords(chunkX, chunkZ);
                if (chunk != null)
                {
                    if (_type == DataType.RequestChangeCell)
                    {
                        subsystemTerrain.ChangeCell(_x, _y, _z, _value);
                    }
                    else
                    {
                        subsystemTerrain.ChangeCellNet(_x, _y, _z, _value);
                    }
                }
            }
                break;
            case DataType.ReplyResult:
                foreach (var p in RelateChunks)
                {
                    var chunk2 = subsystemTerrain.Terrain.GetChunkAtCoords(p.X, p.Y);
                    if (chunk2 == null)
                    {
                        continue;
                    }

                    chunk2.IsRequested = false;
                    chunk2.WasUpgraded = true;
                    chunk2.WasDowngraded = true;
                }

                break;
            case DataType.ChangeCellList:
                foreach (var cellChange in _cellChanges)
                {
                    var chunkX = cellChange.X >> 4;
                    var chunkZ = cellChange.Y >> 4;
                    var chunk = subsystemTerrain.Terrain.GetChunkAtCoords(chunkX, chunkZ);
                    if (chunk != null)
                    {
                        subsystemTerrain.ChangeCellNet(cellChange.X, cellChange.Y, cellChange.Z, cellChange.Value);
                    }
                }

                break;
        }
    }

    public void WriteOneChunk(PackageStreamWriter writer, TerrainChunk chunk)
    {
        writer.Write(chunk.Coords);
        foreach (var cell in chunk.Cells)
        {
            writer.Write(cell);
        }

        foreach (var shaft in chunk.Shafts)
        {
            writer.Write(shaft);
        }
    }

    public void ReadChunks(PackageStreamReader reader)
    {
        var p = reader.ReadPoint2();
        var chunk = new TerrainChunk(null!, p.X, p.Y);
        for (var i = 0; i < chunk.Cells.Length; i++)
        {
            chunk.Cells[i] = reader.ReadInt32();
        }

        for (var i = 0; i < chunk.Shafts.Length; i++)
        {
            chunk.Shafts[i] = reader.ReadInt64();
        }

        Chunks.Add(chunk);
    }

    public void ApplyOneChunk(SubsystemTerrain subsystemTerrain, TerrainChunk chunk)
    {
        var x = chunk.Coords.X;
        var y = chunk.Coords.Y;
        var chunk2 = subsystemTerrain.Terrain.GetChunkAtCoords(x, y) ?? subsystemTerrain.Terrain.AllocateChunk(x, y);
        chunk2.Cells = chunk.Cells;
        chunk2.Shafts = chunk.Shafts;
        chunk2.ThreadState = TerrainChunkState.InvalidLight;
        chunk2.IsRequested = false;
        chunk2.IsLoaded = true;
        chunk2.WasUpgraded = true;
    }
}
