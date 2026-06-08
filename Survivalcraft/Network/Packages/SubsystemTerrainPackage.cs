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

    public readonly List<CellChange> CellChanges = [];

    public int[] Cells = [];

    public List<TerrainChunk> Chunks = [];

    public List<Point3> ModifyCells = [];

    public List<int> ModifyValues = [];

    public DataType Type;

    public int Value;

    public int X;

    public int Y;

    public int Z;

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
        Type = DataType.RequestSyncChunks;
        RelateChunks.AddRange(points);
    }

    public SubsystemTerrainPackage(List<Point2> resultPoints, byte r)
    {
        Type = DataType.ReplyResult;
        RelateChunks.AddRange(resultPoints);
    }

    public SubsystemTerrainPackage(List<TerrainChunk> chunks)
    {
        Type = DataType.SyncTerrainChunkList;
        Chunks.AddRange(chunks);
    }

    public SubsystemTerrainPackage(List<CellChange> changeList)
    {
        CellChanges = changeList;
        Type = DataType.ChangeCellList;
    }

    public SubsystemTerrainPackage(int x, int y, int z, int v, bool request = false)
    {
        Type = request ? DataType.RequestChangeCell : DataType.ChangeCell;
        X = x;
        Y = y;
        Z = z;
        Value = v;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write((byte)Type);
        switch (Type)
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
                writer.Write(X);
                writer.Write(Y);
                writer.Write(Z);
                writer.Write(Value);
                break;
            case DataType.ReplyResult:
                writer.Write((ushort)RelateChunks.Count);
                foreach (var p in RelateChunks)
                {
                    writer.Write(p);
                }

                break;
            case DataType.ChangeCellList:
                writer.Write(CellChanges.Count);
                foreach (var cellChange in CellChanges)
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
        Type = (DataType)reader.ReadByte();
        switch (Type)
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
                X = reader.ReadInt32();
                Y = reader.ReadInt32();
                Z = reader.ReadInt32();
                Value = reader.ReadInt32();
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
                    CellChanges.Add(cellChange);
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
