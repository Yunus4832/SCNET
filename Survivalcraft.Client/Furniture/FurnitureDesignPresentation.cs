using Game.Modding.Blocks;
using Game.World.Presentation;

namespace Game.Furniture;

/// <summary>Client-owned furniture meshes scoped to one presentation catalog and palette world.</summary>
public sealed class FurnitureDesignPresentation(BlockPresentationCatalog catalog) : IDisposable
{
    private readonly BlockPresentationCatalog _catalog = catalog;

    private readonly Lock _cacheLock = new();

    private bool _disposed;

    private readonly Dictionary<FurnitureDesign, List<CachedGeometry>> _cache = new(ReferenceEqualityComparer.Instance);

    public FurnitureGeometry GetGeometry(FurnitureDesign design, SubsystemTerrain? paletteWorld)
    {
        ArgumentNullException.ThrowIfNull(design);
        if (design.ContentCatalog is { } rules && !ReferenceEquals(rules, _catalog.Rules) ||
            paletteWorld is not null && !ReferenceEquals(paletteWorld.Catalog, _catalog.Rules))
        {
            throw new ArgumentException("Furniture presentation must use its own rule catalog and palette world.");
        }

        lock (_cacheLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_cache.TryGetValue(design, out var entries))
            {
                entries = [];
                _cache.Add(design, entries);
            }

            var index = entries.FindIndex(entry => ReferenceEquals(entry.PaletteWorld, paletteWorld));
            if (index >= 0 && entries[index].Revision == design.Revision) { return entries[index].Geometry; }

            var geometry = BuildGeometry(design, paletteWorld);
            var cached = new CachedGeometry(design.Revision, paletteWorld, geometry);
            if (index >= 0) { entries[index] = cached; }
            else { entries.Add(cached); }

            return geometry;
        }
    }

    public void Dispose()
    {
        lock (_cacheLock)
        {
            _disposed = true;
            _cache.Clear();
        }
    }

    private sealed record CachedGeometry(long Revision, SubsystemTerrain? PaletteWorld, FurnitureGeometry Geometry);

    private struct Cell
    {
        public int Value;
    }

    public string GetDefaultName(FurnitureDesign design)
    {
        if (design.InteractionMode == FurnitureInteractionMode.Multistate)
        {
            var count = design.ListChain().Count;
            if (count > 1)
            {
                return string.Format(Localization.ClientLanguage.Catalog.Get(nameof(FurnitureDesign), 5), count);
            }
        }
        else
        {
            if (design.InteractionMode == FurnitureInteractionMode.ElectricButton)
            {
                return Localization.ClientLanguage.Catalog.Get(nameof(FurnitureDesign), 6);
            }

            if (design.InteractionMode == FurnitureInteractionMode.ElectricSwitch)
            {
                return Localization.ClientLanguage.Catalog.Get(nameof(FurnitureDesign), 7);
            }

            if (design.InteractionMode != FurnitureInteractionMode.ConnectedMultistate)
            {
                return Localization.ClientLanguage.Catalog.Get(nameof(FurnitureDesign), 9);
            }

            var count2 = design.ListChain().Count;
            if (count2 > 1)
            {
                return string.Format(Localization.ClientLanguage.Catalog.Get(nameof(FurnitureDesign), 8), count2);
            }
        }

        return Localization.ClientLanguage.Catalog.Get(nameof(FurnitureDesign), 9);
    }

    private FurnitureGeometry BuildGeometry(FurnitureDesign design, SubsystemTerrain? terrain)
    {
        var resolution = design.Resolution;
        var geometry = new FurnitureGeometry();
        for (var i = 0; i < 6; i++)
        {
            var num = CellFace.OppositeFace(i);
            Point3 point;
            Point3 point2;
            Point3 point3;
            Point3 point4;
            Point3 point5;
            switch (i)
            {
                case 0:
                    point = new Point3(0, 0, 1);
                    point2 = new Point3(-1, 0, 0);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(resolution, resolution, 0);
                    point5 = new Point3(resolution - 1, resolution - 1, 0);
                    break;
                case 1:
                    point = new Point3(1, 0, 0);
                    point2 = new Point3(0, 0, 1);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(0, resolution, 0);
                    point5 = new Point3(0, resolution - 1, 0);
                    break;
                case 2:
                    point = new Point3(0, 0, -1);
                    point2 = new Point3(1, 0, 0);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(0, resolution, resolution);
                    point5 = new Point3(0, resolution - 1, resolution - 1);
                    break;
                case 3:
                    point = new Point3(-1, 0, 0);
                    point2 = new Point3(0, 0, -1);
                    point3 = new Point3(0, -1, 0);
                    point4 = new Point3(resolution, resolution, resolution);
                    point5 = new Point3(resolution - 1, resolution - 1, resolution - 1);
                    break;
                case 4:
                    point = new Point3(0, 1, 0);
                    point2 = new Point3(-1, 0, 0);
                    point3 = new Point3(0, 0, 1);
                    point4 = new Point3(resolution, 0, 0);
                    point5 = new Point3(resolution - 1, 0, 0);
                    break;
                default:
                    point = new Point3(0, -1, 0);
                    point2 = new Point3(-1, 0, 0);
                    point3 = new Point3(0, 0, -1);
                    point4 = new Point3(resolution, resolution, resolution);
                    point5 = new Point3(resolution - 1, resolution - 1, resolution - 1);
                    break;
            }

            var blockMesh = new BlockMesh();
            var blockMesh2 = new BlockMesh();
            for (var j = 0; j < resolution; j++)
            {
                var array = new Cell[resolution * resolution];
                for (var k = 0; k < resolution; k++)
                {
                    for (var l = 0; l < resolution; l++)
                    {
                        var num2 = j * point.X + k * point3.X + l * point2.X + point5.X;
                        var num3 = j * point.Y + k * point3.Y + l * point2.Y + point5.Y;
                        var num4 = j * point.Z + k * point3.Z + l * point2.Z + point5.Z;
                        var num5 = num2 + num3 * resolution + num4 * resolution * resolution;
                        var num6 = design.GetValue(num5);
                        Cell cell = default;
                        cell.Value = num6;
                        var cell2 = cell;
                        if (j > 0 && num6 != 0)
                        {
                            var num7 = num2 -
                                       point.X +
                                       (num3 - point.Y) * resolution +
                                       (num4 - point.Z) * resolution * resolution;
                            var value = design.GetValue(num7);
                            if (!FurnitureDesign.IsValueTransparent(value) ||
                                Terrain.ExtractContents(num6) == Terrain.ExtractContents(value))
                            {
                                cell2.Value = 0;
                            }
                        }

                        array[l + k * resolution] = cell2;
                    }
                }

                for (var m = 0; m < resolution; m++)
                {
                    for (var n = 0; n < resolution; n++)
                    {
                        var value2 = array[n + m * resolution].Value;
                        if (value2 == 0)
                        {
                            continue;
                        }

                        var point6 = FindLargestSize(array, new Point2(n, m), value2, resolution);
                        if (point6 == Point2.Zero)
                        {
                            continue;
                        }

                        MarkUsed(array, new Point2(n, m), point6, resolution);
                        var num8 = 0.0005f * resolution;
                        var num9 = n - num8;
                        var num10 = n + point6.X + num8;
                        var num11 = m - num8;
                        var num12 = m + point6.Y + num8;
                        var x = j * point.X + num11 * point3.X + num9 * point2.X + point4.X;
                        var y = j * point.Y + num11 * point3.Y + num9 * point2.Y + point4.Y;
                        var z = j * point.Z + num11 * point3.Z + num9 * point2.Z + point4.Z;
                        var x2 = j * point.X + num11 * point3.X + num10 * point2.X + point4.X;
                        var y2 = j * point.Y + num11 * point3.Y + num10 * point2.Y + point4.Y;
                        var z2 = j * point.Z + num11 * point3.Z + num10 * point2.Z + point4.Z;
                        var x3 = j * point.X + num12 * point3.X + num10 * point2.X + point4.X;
                        var y3 = j * point.Y + num12 * point3.Y + num10 * point2.Y + point4.Y;
                        var z3 = j * point.Z + num12 * point3.Z + num10 * point2.Z + point4.Z;
                        var x4 = j * point.X + num12 * point3.X + num9 * point2.X + point4.X;
                        var y4 = j * point.Y + num12 * point3.Y + num9 * point2.Y + point4.Y;
                        var z4 = j * point.Z + num12 * point3.Z + num9 * point2.Z + point4.Z;
                        var blockMesh3 = blockMesh;
                        var num13 = Terrain.ExtractContents(value2);
                        var block = _catalog.Rules.Blocks[num13];
                        var num14 = _catalog.Get(num13).GetFaceTextureSlot(i, value2);
                        var isEmissive = false;
                        var color = Color.White;
                        if (block is IPaintableBlock paintableBlock)
                        {
                            var paintColor = paintableBlock.GetPaintColor(value2);
                            color = PalettePresentation.GetColor(terrain, paintColor);
                        }
                        else if (block is WaterBlock)
                        {
                            color = BlockColorsMap.WaterColorsMap.Lookup(12, 12);
                            num14 = 189;
                        }
                        else if (block is CarpetBlock)
                        {
                            var color2 = CarpetBlock.GetColor(Terrain.ExtractData(value2));
                            color = PalettePresentation.GetFabricColor(terrain, color2);
                        }
                        else if (block is TorchBlock or WickerLampBlock)
                        {
                            isEmissive = true;
                            num14 = 31;
                        }
                        else if (block is GlassBlock)
                        {
                            blockMesh3 = blockMesh2;
                        }

                        var num15 = num14 % 16;
                        var num16 = num14 / 16;
                        var count = blockMesh3.Vertices.Count;
                        blockMesh3.Vertices.Count += 4;
                        var array2 = blockMesh3.Vertices.Array;
                        var x5 = ((n + 0.01f) / resolution + num15) / 16f;
                        var x6 = ((n + point6.X - 0.01f) / resolution + num15) / 16f;
                        var y5 = ((m + 0.01f) / resolution + num16) / 16f;
                        var y6 = ((m + point6.Y - 0.01f) / resolution + num16) / 16f;
                        array2[count] = new BlockMeshVertex
                        {
                            Position = new Vector3(x, y, z) / resolution,
                            Color = color,
                            Face = (byte)num,
                            TextureCoordinates = new Vector2(x5, y5),
                            IsEmissive = isEmissive
                        };
                        array2[count + 1] = new BlockMeshVertex
                        {
                            Position = new Vector3(x2, y2, z2) / resolution,
                            Color = color,
                            Face = (byte)num,
                            TextureCoordinates = new Vector2(x6, y5),
                            IsEmissive = isEmissive
                        };
                        array2[count + 2] = new BlockMeshVertex
                        {
                            Position = new Vector3(x3, y3, z3) / resolution,
                            Color = color,
                            Face = (byte)num,
                            TextureCoordinates = new Vector2(x6, y6),
                            IsEmissive = isEmissive
                        };
                        array2[count + 3] = new BlockMeshVertex
                        {
                            Position = new Vector3(x4, y4, z4) / resolution,
                            Color = color,
                            Face = (byte)num,
                            TextureCoordinates = new Vector2(x5, y6),
                            IsEmissive = isEmissive
                        };
                        var count2 = blockMesh3.Indices.Count;
                        blockMesh3.Indices.Count += 6;
                        var array3 = blockMesh3.Indices.Array;
                        array3[count2] = (ushort)count;
                        array3[count2 + 1] = (ushort)(count + 1);
                        array3[count2 + 2] = (ushort)(count + 2);
                        array3[count2 + 3] = (ushort)(count + 2);
                        array3[count2 + 4] = (ushort)(count + 3);
                        array3[count2 + 5] = (ushort)count;
                    }
                }
            }

            if (blockMesh.Indices.Count > 0)
            {
                blockMesh.Trim();
                blockMesh.GenerateSidesData();
                geometry.SubsetOpaqueByFace[i] = blockMesh;
            }

            if (blockMesh2.Indices.Count > 0)
            {
                blockMesh2.Trim();
                blockMesh2.GenerateSidesData();
                geometry.SubsetAlphaTestByFace[i] = blockMesh2;
            }
        }

        return geometry;
    }

    private static Point2 FindLargestSize(Cell[] surface, Point2 start, int value, int resolution)
    {
        var result = Point2.Zero;
        var num = resolution;
        for (var i = start.Y; i < resolution; i++)
        {
            for (var j = start.X; j <= num; j++)
            {
                if (j == num || surface[j + i * resolution].Value != value)
                {
                    num = j;
                    var point = new Point2(num - start.X, i - start.Y + 1);
                    if (point.X * point.Y > result.X * result.Y)
                    {
                        result = point;
                    }
                }
            }
        }

        return result;
    }

    private static void MarkUsed(Cell[] surface, Point2 start, Point2 size, int resolution)
    {
        for (var i = start.Y; i < start.Y + size.Y; i++)
        {
            for (var j = start.X; j < start.X + size.X; j++)
            {
                surface[j + i * resolution].Value = 0;
            }
        }
    }
}
