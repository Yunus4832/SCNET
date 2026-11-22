namespace Game.Terrains;

public class TerrainBrush
{
    private Dictionary<int, Cell> _cellsDictionary = new();

    public Cell[] Cells { get; private set; } = [];

    public static int Key(int x, int y, int z)
    {
        return y + 128 + ((x + 128) << 8) + ((z + 128) << 16);
    }

    public void Compile()
    {
        Cells = new Cell[_cellsDictionary.Values.Count];
        var writeIndex = 0;
        foreach (var value in _cellsDictionary.Values)
        {
            Cells[writeIndex++] = value;
        }

        Array.Sort(Cells);
        _cellsDictionary = new Dictionary<int, Cell>();
    }

    public int CountNonDiagonalNeighbors(int x, int y, int z, Counter counter)
    {
        return 0 +
               counter.Count(this, new Point3(x - 1, y, z)) +
               counter.Count(this, new Point3(x + 1, y, z)) +
               counter.Count(this, new Point3(x, y - 1, z)) +
               counter.Count(this, new Point3(x, y + 1, z)) +
               counter.Count(this, new Point3(x, y, z - 1)) +
               counter.Count(this, new Point3(x, y, z + 1));
    }

    public int CountBox(int x, int y, int z, int sizeX, int sizeY, int sizeZ, Counter counter)
    {
        var sum = 0;
        for (var i = x; i < x + sizeX; i++)
        {
            for (var j = y; j < y + sizeY; j++)
            {
                for (var k = z; k < z + sizeZ; k++)
                {
                    sum += counter.Count(this, new Point3(i, j, k));
                }
            }
        }

        return sum;
    }

    public void Replace(int oldValue, int newValue)
    {
        var dictionary = new Dictionary<int, Cell>();
        foreach (var item in _cellsDictionary)
        {
            var value = item.Value;
            if (value.Value == oldValue)
            {
                value.Value = newValue;
            }

            dictionary[item.Key] = value;
        }

        _cellsDictionary = dictionary;
        Cells = [];
    }

    public void CalculateBounds(out Point3 min, out Point3 max)
    {
        min = Point3.Zero;
        max = Point3.Zero;
        var isFirstCell = true;
        foreach (var value in _cellsDictionary.Values)
        {
            if (isFirstCell)
            {
                isFirstCell = false;
                min.X = max.X = value.X;
                min.Y = max.Y = value.Y;
                min.Z = max.Z = value.Z;
            }
            else
            {
                min.X = MathUtils.Min(min.X, value.X);
                min.Y = MathUtils.Min(min.Y, value.Y);
                min.Z = MathUtils.Min(min.Z, value.Z);
                max.X = MathUtils.Max(max.X, value.X);
                max.Y = MathUtils.Max(max.Y, value.Y);
                max.Z = MathUtils.Max(max.Z, value.Z);
            }
        }
    }

    public int? GetValue(Point3 p)
    {
        return GetValue(p.X, p.Y, p.Z);
    }

    public int? GetValue(int x, int y, int z)
    {
        var key = Key(x, y, z);
        if (_cellsDictionary.TryGetValue(key, out var value))
        {
            return value.Value;
        }

        return null;
    }

    public void AddCell(int x, int y, int z, Brush brush)
    {
        var paintValue = brush.Paint(this, new Point3(x, y, z));
        if (!paintValue.HasValue)
        {
            return;
        }

        var key = Key(x, y, z);
        _cellsDictionary[key] = new Cell
        {
            X = (sbyte)x,
            Y = (sbyte)y,
            Z = (sbyte)z,
            Value = paintValue.Value
        };
        Cells = [];
    }

    public void AddBox(int x, int y, int z, int sizeX, int sizeY, int sizeZ, Brush brush)
    {
        for (var i = x; i < x + sizeX; i++)
        {
            for (var j = y; j < y + sizeY; j++)
            {
                for (var k = z; k < z + sizeZ; k++)
                {
                    AddCell(i, j, k, brush);
                }
            }
        }
    }

    public void AddRay(int x1, int y1, int z1, int x2, int y2, int z2, int sizeX, int sizeY, int sizeZ, Brush brush)
    {
        var rayStart = new Vector3(x1, y1, z1) + new Vector3(0.5f);
        var rayEnd = new Vector3(x2, y2, z2) + new Vector3(0.5f);
        var step = 0.33f * Vector3.Normalize(rayEnd - rayStart);
        var sampleCount = (int)MathUtils.Round(3f * Vector3.Distance(rayStart, rayEnd));
        var samplePosition = rayStart;
        for (var i = 0; i < sampleCount; i++)
        {
            var x3 = Terrain.ToCell(samplePosition.X);
            var y3 = Terrain.ToCell(samplePosition.Y);
            var z3 = Terrain.ToCell(samplePosition.Z);
            AddBox(x3, y3, z3, sizeX, sizeY, sizeZ, brush);
            samplePosition += step;
        }
    }

    public void PaintFastSelective(TerrainChunk chunk, int x, int y, int z, int onlyInValue)
    {
        x -= chunk.Origin.X;
        z -= chunk.Origin.Y;
        var cells = Cells;
        foreach (var cell in cells)
        {
            var localX = cell.X + x;
            var cellY = cell.Y + y;
            var localZ = cell.Z + z;
            if (localX is < 0 or >= 16 || cellY is < 0 or >= 505 || localZ is < 0 or >= 16)
            {
                continue;
            }

            var index = TerrainChunk.CalculateCellIndex(localX, cellY, localZ);
            var cellValueFast = chunk.GetCellValueFast(index);
            if (onlyInValue == cellValueFast)
            {
                chunk.SetCellValueFast(index, cell.Value);
            }
        }
    }

    public void PaintFastSelective(
        Terrain terrain,
        int x,
        int y,
        int z,
        int minX,
        int maxX,
        int minY,
        int maxY,
        int minZ,
        int maxZ,
        int onlyInValue
    )
    {
        var cells = Cells;
        foreach (var cell in cells)
        {
            var worldX = cell.X + x;
            var worldY = cell.Y + y;
            var worldZ = cell.Z + z;
            if (worldX < minX || worldX >= maxX || worldY < minY || worldY >= maxY || worldZ < minZ || worldZ >= maxZ)
            {
                continue;
            }

            var cellValueFast = terrain.GetCellValueFast(worldX, worldY, worldZ);
            if (onlyInValue == cellValueFast)
            {
                terrain.SetCellValueFast(worldX, worldY, worldZ, cell.Value);
            }
        }
    }

    public void PaintFastAvoidWater(TerrainChunk chunk, int x, int y, int z)
    {
        var terrain = chunk.Terrain;
        x -= chunk.Origin.X;
        z -= chunk.Origin.Y;
        var cells = Cells;
        foreach (var cell in cells)
        {
            var localX = cell.X + x;
            var cellY = cell.Y + y;
            var localZ = cell.Z + z;
            if (localX < 0 || localX >= 16 || cellY < 0 || cellY >= 505 || localZ < 0 || localZ >= 16)
            {
                continue;
            }

            var worldX = localX + chunk.Origin.X;
            var worldZ = localZ + chunk.Origin.Y;
            if (chunk.GetCellContentsFast(localX, cellY, localZ) != 18 &&
                terrain.GetCellContents(worldX - 1, cellY, worldZ) != 18 &&
                terrain.GetCellContents(worldX + 1, cellY, worldZ) != 18 &&
                terrain.GetCellContents(worldX, cellY, worldZ - 1) != 18 &&
                terrain.GetCellContents(worldX, cellY, worldZ + 1) != 18 &&
                chunk.GetCellContentsFast(localX, cellY + 1, localZ) != 18)
            {
                chunk.SetCellValueFast(localX, cellY, localZ, cell.Value);
            }
        }
    }

    public void PaintFastAvoidWater(
        Terrain terrain,
        int x,
        int y,
        int z,
        int minX,
        int maxX,
        int minY,
        int maxY,
        int minZ,
        int maxZ
    )
    {
        var cells = Cells;
        foreach (var cell in cells)
        {
            var worldX = cell.X + x;
            var worldY = cell.Y + y;
            var worldZ = cell.Z + z;
            if (worldX >= minX &&
                worldX < maxX &&
                worldY >= minY &&
                worldY < maxY &&
                worldZ >= minZ &&
                worldZ < maxZ &&
                terrain.GetCellContentsFast(worldX, worldY, worldZ) != 18 &&
                terrain.GetCellContents(worldX - 1, worldY, worldZ) != 18 &&
                terrain.GetCellContents(worldX + 1, worldY, worldZ) != 18 &&
                terrain.GetCellContents(worldX, worldY, worldZ - 1) != 18 &&
                terrain.GetCellContents(worldX, worldY, worldZ + 1) != 18 &&
                terrain.GetCellContentsFast(worldX, worldY + 1, worldZ) != 18)
            {
                terrain.SetCellValueFast(worldX, worldY, worldZ, cell.Value);
            }
        }
    }

    public void PaintFast(TerrainChunk chunk, int x, int y, int z)
    {
        x -= chunk.Origin.X;
        z -= chunk.Origin.Y;
        var cells = Cells;
        foreach (var cell in cells)
        {
            var xPosition = cell.X + x;
            var yPosition = cell.Y + y;
            var zPosition = cell.Z + z;
            if (xPosition < 0 ||
                xPosition >= 16 ||
                yPosition < 0 ||
                yPosition >= 500 ||
                zPosition < 0 ||
                zPosition >= 16)
            {
                continue;
            }

            chunk.SetCellValueFast(xPosition, yPosition, zPosition, cell.Value);
        }
    }

    public void PaintFast(
        Terrain terrain,
        int x,
        int y,
        int z,
        int minX,
        int maxX,
        int minY,
        int maxY,
        int minZ,
        int maxZ
    )
    {
        var cells = Cells;
        foreach (var cell in cells)
        {
            var xPosition = cell.X + x;
            var yPosition = cell.Y + y;
            var zPosition = cell.Z + z;
            if (xPosition >= minX &&
                xPosition < maxX &&
                yPosition >= minY &&
                yPosition < maxY &&
                zPosition >= minZ &&
                zPosition < maxZ)
            {
                terrain.SetCellValueFast(xPosition, yPosition, zPosition, cell.Value);
            }
        }
    }

    public void Paint(SubsystemTerrain terrain, int x, int y, int z)
    {
        var cells = Cells;
        foreach (var cell in cells)
        {
            var xPosition = cell.X + x;
            var yPosition = cell.Y + y;
            var zPosition = cell.Z + z;
            terrain.ChangeCell(xPosition, yPosition, zPosition, cell.Value);
        }
    }

    public struct Cell : IComparable<Cell>
    {
        public sbyte X;

        public sbyte Y;

        public sbyte Z;

        public int Value;

        public int CompareTo(Cell other)
        {
            return Key(X, Y, Z) - Key(other.X, other.Y, other.Z);
        }
    }

    public class Brush
    {
        private Func<int?, int?>? _handler1;

        private Func<Point3, int?>? _handler2;

        private int _value;

        public static implicit operator Brush(int value)
        {
            return new Brush
            {
                _value = value
            };
        }

        public static implicit operator Brush(Func<int?, int?> handler)
        {
            return new Brush
            {
                _handler1 = handler
            };
        }

        public static implicit operator Brush(Func<Point3, int?> handler)
        {
            return new Brush
            {
                _handler2 = handler
            };
        }

        public int? Paint(TerrainBrush terrainBrush, Point3 p)
        {
            if (_handler1 != null)
            {
                return _handler1(terrainBrush.GetValue(p.X, p.Y, p.Z));
            }

            return _handler2 != null ? _handler2(p) : _value;
        }
    }

    public class Counter
    {
        private Func<int?, int>? _handler1;

        private Func<Point3, int>? _handler2;

        private int _value;

        public static implicit operator Counter(int value)
        {
            return new Counter
            {
                _value = value
            };
        }

        public static implicit operator Counter(Func<int?, int> handler)
        {
            return new Counter
            {
                _handler1 = handler
            };
        }

        public static implicit operator Counter(Func<Point3, int> handler)
        {
            return new Counter
            {
                _handler2 = handler
            };
        }

        public int Count(TerrainBrush terrainBrush, Point3 p)
        {
            if (_handler1 != null)
            {
                return _handler1(terrainBrush.GetValue(p));
            }

            if (_handler2 != null)
            {
                return _handler2(p);
            }

            if (terrainBrush.GetValue(p) != _value)
            {
                return 0;
            }

            return 1;
        }
    }
}
