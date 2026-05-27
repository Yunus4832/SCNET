using System.Runtime.InteropServices;

using Engine.Graphics;
using Engine.Media;

namespace Game;

public class BlockMesh
{
    public readonly DynamicArray<ushort> Indices = [];

    public DynamicArray<sbyte> Sides = [];

    public readonly DynamicArray<BlockMeshVertex> Vertices = [];

    public BoundingBox CalculateBoundingBox()
    {
        return new BoundingBox(Vertices.Select(v => v.Position));
    }

    public BoundingBox CalculateBoundingBox(Matrix matrix)
    {
        return new BoundingBox(Vertices.Select(v => Vector3.Transform(v.Position, matrix)));
    }

    public static Matrix GetBoneAbsoluteTransform(ModelBone modelBone)
    {
        if (modelBone.ParentBone != null)
        {
            return GetBoneAbsoluteTransform(modelBone.ParentBone) * modelBone.Transform;
        }

        return modelBone.Transform;
    }

    public void AppendImageExtrusion(Image image, Rectangle bounds, Vector3 size, Color color)
    {
        var blockMesh = new BlockMesh();
        var vertices = blockMesh.Vertices;
        var indices = blockMesh.Indices;
        var item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Left, bounds.Top, -1f),
            TextureCoordinates = new Vector2(bounds.Left, bounds.Top)
        };
        vertices.Add(item);
        item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Right, bounds.Top, -1f),
            TextureCoordinates = new Vector2(bounds.Right, bounds.Top)
        };
        vertices.Add(item);
        item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Left, bounds.Bottom, -1f),
            TextureCoordinates = new Vector2(bounds.Left, bounds.Bottom)
        };
        vertices.Add(item);
        item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Right, bounds.Bottom, -1f),
            TextureCoordinates = new Vector2(bounds.Right, bounds.Bottom)
        };
        vertices.Add(item);
        indices.Add((ushort)(vertices.Count - 4));
        indices.Add((ushort)(vertices.Count - 1));
        indices.Add((ushort)(vertices.Count - 3));
        indices.Add((ushort)(vertices.Count - 1));
        indices.Add((ushort)(vertices.Count - 4));
        indices.Add((ushort)(vertices.Count - 2));
        item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Left, bounds.Top, 1f),
            TextureCoordinates = new Vector2(bounds.Left, bounds.Top)
        };
        vertices.Add(item);
        item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Right, bounds.Top, 1f),
            TextureCoordinates = new Vector2(bounds.Right, bounds.Top)
        };
        vertices.Add(item);
        item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Left, bounds.Bottom, 1f),
            TextureCoordinates = new Vector2(bounds.Left, bounds.Bottom)
        };
        vertices.Add(item);
        item = new BlockMeshVertex
        {
            Position = new Vector3(bounds.Right, bounds.Bottom, 1f),
            TextureCoordinates = new Vector2(bounds.Right, bounds.Bottom)
        };
        vertices.Add(item);
        indices.Add((ushort)(vertices.Count - 4));
        indices.Add((ushort)(vertices.Count - 3));
        indices.Add((ushort)(vertices.Count - 1));
        indices.Add((ushort)(vertices.Count - 1));
        indices.Add((ushort)(vertices.Count - 2));
        indices.Add((ushort)(vertices.Count - 4));
        for (var i = bounds.Left - 1; i <= bounds.Right; i++)
        {
            var num = -1;
            for (var j = bounds.Top - 1; j <= bounds.Bottom; j++)
            {
                var num2 = !bounds.Contains(new Point2(i, j)) || image.GetPixel(i, j) == Color.Transparent;
                var flag = bounds.Contains(new Point2(i - 1, j)) && image.GetPixel(i - 1, j) != Color.Transparent;
                if (num2 & flag)
                {
                    if (num < 0)
                    {
                        num = j;
                    }
                }
                else if (num >= 0)
                {
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(i - 0.01f, num - 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(i - 1 + 0.01f, num + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(i - 0.01f, num - 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(i - 0.01f, num + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(i - 0.01f, j + 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(i - 1 + 0.01f, j - 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(i - 0.01f, j + 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(i - 0.01f, j - 0.01f)
                    };
                    vertices.Add(item);
                    indices.Add((ushort)(vertices.Count - 4));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 3));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 4));
                    indices.Add((ushort)(vertices.Count - 2));
                    num = -1;
                }
            }
        }

        for (var k = bounds.Left - 1; k <= bounds.Right; k++)
        {
            var num3 = -1;
            for (var l = bounds.Top - 1; l <= bounds.Bottom; l++)
            {
                var num4 = !bounds.Contains(new Point2(k, l)) || image.GetPixel(k, l) == Color.Transparent;
                var flag2 = bounds.Contains(new Point2(k + 1, l)) && image.GetPixel(k + 1, l) != Color.Transparent;
                if (num4 & flag2)
                {
                    if (num3 < 0)
                    {
                        num3 = l;
                    }
                }
                else if (num3 >= 0)
                {
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(k + 1 + 0.01f, num3 - 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(k + 1 + 0.01f, num3 + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(k + 1 + 0.01f, num3 - 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(k + 2 - 0.01f, num3 + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(k + 1 + 0.01f, l + 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(k + 1 + 0.01f, l - 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(k + 1 + 0.01f, l + 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(k + 2 - 0.01f, l - 0.01f)
                    };
                    vertices.Add(item);
                    indices.Add((ushort)(vertices.Count - 4));
                    indices.Add((ushort)(vertices.Count - 3));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 2));
                    indices.Add((ushort)(vertices.Count - 4));
                    num3 = -1;
                }
            }
        }

        for (var m = bounds.Top - 1; m <= bounds.Bottom; m++)
        {
            var num5 = -1;
            for (var n = bounds.Left - 1; n <= bounds.Right; n++)
            {
                var num6 = !bounds.Contains(new Point2(n, m)) || image.GetPixel(n, m) == Color.Transparent;
                var flag3 = bounds.Contains(new Point2(n, m - 1)) && image.GetPixel(n, m - 1) != Color.Transparent;
                if (num6 & flag3)
                {
                    if (num5 < 0)
                    {
                        num5 = n;
                    }
                }
                else if (num5 >= 0)
                {
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(num5 - 0.01f, m - 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(num5 + 0.01f, m - 1 + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(num5 - 0.01f, m - 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(num5 + 0.01f, m - 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(n + 0.01f, m - 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(n - 0.01f, m - 1 + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(n + 0.01f, m - 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(n - 0.01f, m - 0.01f)
                    };
                    vertices.Add(item);
                    indices.Add((ushort)(vertices.Count - 4));
                    indices.Add((ushort)(vertices.Count - 3));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 2));
                    indices.Add((ushort)(vertices.Count - 4));
                    num5 = -1;
                }
            }
        }

        for (var num7 = bounds.Top - 1; num7 <= bounds.Bottom; num7++)
        {
            var num8 = -1;
            for (var num9 = bounds.Left - 1; num9 <= bounds.Right; num9++)
            {
                var num10 = !bounds.Contains(new Point2(num9, num7)) || image.GetPixel(num9, num7) == Color.Transparent;
                var flag4 = bounds.Contains(new Point2(num9, num7 + 1)) &&
                            image.GetPixel(num9, num7 + 1) != Color.Transparent;
                if (num10 & flag4)
                {
                    if (num8 < 0)
                    {
                        num8 = num9;
                    }
                }
                else if (num8 >= 0)
                {
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(num8 - 0.01f, num7 + 1 + 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(num8 + 0.01f, num7 + 1 + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(num8 - 0.01f, num7 + 1 + 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(num8 + 0.01f, num7 + 2 - 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(num9 + 0.01f, num7 + 1 + 0.01f, -1.01f),
                        TextureCoordinates = new Vector2(num9 - 0.01f, num7 + 1 + 0.01f)
                    };
                    vertices.Add(item);
                    item = new BlockMeshVertex
                    {
                        Position = new Vector3(num9 + 0.01f, num7 + 1 + 0.01f, 1.01f),
                        TextureCoordinates = new Vector2(num9 - 0.01f, num7 + 2 - 0.01f)
                    };
                    vertices.Add(item);
                    indices.Add((ushort)(vertices.Count - 4));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 3));
                    indices.Add((ushort)(vertices.Count - 1));
                    indices.Add((ushort)(vertices.Count - 4));
                    indices.Add((ushort)(vertices.Count - 2));
                    num8 = -1;
                }
            }
        }

        for (var num11 = 0; num11 < vertices.Count; num11++)
        {
            vertices.Array[num11].Position.X -= bounds.Left + bounds.Width / 2f;
            vertices.Array[num11].Position.Y = bounds.Bottom - vertices.Array[num11].Position.Y - bounds.Height / 2f;
            vertices.Array[num11].Position.X *= size.X / bounds.Width;
            vertices.Array[num11].Position.Y *= size.Y / bounds.Height;
            vertices.Array[num11].Position.Z *= size.Z / 2f;
            vertices.Array[num11].TextureCoordinates.X /= image.Width;
            vertices.Array[num11].TextureCoordinates.Y /= image.Height;
            vertices.Array[num11].Color = color;
        }

        AppendBlockMesh(blockMesh);
    }

    public void AppendModelMeshPart(ModelMeshPart meshPart, Matrix matrix, bool makeEmissive, bool flipWindingOrder,
        bool doubleSided, bool flipNormals, Color color)
    {
        var vertexBuffer = meshPart.VertexBuffer;
        var indexBuffer = meshPart.IndexBuffer;
        var vertexElements = vertexBuffer.VertexDeclaration.VertexElements;
        if (vertexElements.Count != 3 || vertexElements[0].Offset != 0 ||
            vertexElements[0].Semantic != VertexElementSemantic.Position.GetSemanticString() ||
            vertexElements[1].Offset != 12 ||
            vertexElements[1].Semantic != VertexElementSemantic.Normal.GetSemanticString() ||
            vertexElements[2].Offset != 24 ||
            vertexElements[2].Semantic != VertexElementSemantic.TextureCoordinate.GetSemanticString())
        {
            throw new InvalidOperationException("Wrong vertex format for a block mesh.");
        }

        var vertexData = GetVertexData<InternalVertex>(vertexBuffer);
        var indexData = GetIndexData<ushort>(indexBuffer);
        var dictionary = new Dictionary<ushort, ushort>();
        for (var i = meshPart.StartIndex; i < meshPart.StartIndex + meshPart.IndicesCount; i++)
        {
            var num = indexData[i];
            if (!dictionary.ContainsKey(num))
            {
                dictionary.Add(num, (ushort)Vertices.Count);
                BlockMeshVertex item = default;
                item.Position = Vector3.Transform(vertexData[num].Position, matrix);
                item.TextureCoordinates = vertexData[num].TextureCoordinate;
                var vector =
                    Vector3.Normalize(
                        Vector3.TransformNormal(flipNormals ? -vertexData[num].Normal : vertexData[num].Normal,
                            matrix));
                if (makeEmissive)
                {
                    item.IsEmissive = true;
                    item.Color = color;
                }
                else
                {
                    item.Color = color * LightingManager.CalculateLighting(vector);
                    item.Color.A = color.A;
                }

                item.Face = (byte)CellFace.Vector3ToFace(vector);
                Vertices.Add(item);
            }
        }

        for (var j = 0; j < meshPart.IndicesCount / 3; j++)
        {
            if (doubleSided)
            {
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 1]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 2]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 2]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 1]]);
            }
            else if (flipWindingOrder)
            {
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 2]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 1]]);
            }
            else
            {
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 1]]);
                Indices.Add(dictionary[indexData[meshPart.StartIndex + 3 * j + 2]]);
            }
        }

        Trim();
    }

    public void AppendBlockMesh(BlockMesh blockMesh)
    {
        var count = Vertices.Count;
        for (var i = 0; i < blockMesh.Vertices.Count; i++)
        {
            Vertices.Add(blockMesh.Vertices.Array[i]);
        }

        for (var j = 0; j < blockMesh.Indices.Count; j++)
        {
            Indices.Add((ushort)(blockMesh.Indices.Array[j] + count));
        }

        Trim();
    }

    public void BlendBlockMesh(BlockMesh blockMesh, float factor)
    {
        if (blockMesh.Vertices.Count != Vertices.Count)
        {
            throw new InvalidOperationException("Meshes do not match.");
        }

        for (var i = 0; i < Vertices.Count; i++)
        {
            var position = Vertices.Array[i].Position;
            var position2 = blockMesh.Vertices.Array[i].Position;
            Vertices.Array[i].Position = Vector3.Lerp(position, position2, factor);
        }
    }

    public void TransformPositions(Matrix matrix, int facesMask = -1)
    {
        for (var i = 0; i < Vertices.Count; i++)
        {
            if (((1 << Vertices.Array[i].Face) & facesMask) != 0)
            {
                Vertices.Array[i].Position = Vector3.Transform(Vertices.Array[i].Position, matrix);
            }
        }
    }

    public void TransformTextureCoordinates(Matrix matrix, int facesMask = -1)
    {
        for (var i = 0; i < Vertices.Count; i++)
        {
            if (((1 << Vertices.Array[i].Face) & facesMask) != 0)
            {
                Vertices.Array[i].TextureCoordinates = Vector2.Transform(Vertices.Array[i].TextureCoordinates, matrix);
            }
        }
    }

    public void SetColor(Color color, int facesMask = -1)
    {
        for (var i = 0; i < Vertices.Count; i++)
        {
            if (((1 << Vertices.Array[i].Face) & facesMask) != 0)
            {
                Vertices.Array[i].Color = color;
            }
        }
    }

    public void ModulateColor(Color color, int facesMask = -1)
    {
        for (var i = 0; i < Vertices.Count; i++)
        {
            if (((1 << Vertices.Array[i].Face) & facesMask) != 0)
            {
                Vertices.Array[i].Color *= color;
            }
        }
    }

    public void GenerateSidesData()
    {
        Sides = new DynamicArray<sbyte>();
        Sides.Count = Indices.Count / 3;
        for (var i = 0; i < Sides.Count; i++)
        {
            int num = Indices.Array[3 * i];
            int num2 = Indices.Array[3 * i + 1];
            int num3 = Indices.Array[3 * i + 2];
            var position = Vertices.Array[num].Position;
            var position2 = Vertices.Array[num2].Position;
            var position3 = Vertices.Array[num3].Position;
            if (IsNear(position.Z, position2.Z, position3.Z, 1f))
            {
                Sides.Array[i] = 0;
            }
            else if (IsNear(position.X, position2.X, position3.X, 1f))
            {
                Sides.Array[i] = 1;
            }
            else if (IsNear(position.Z, position2.Z, position3.Z, 0f))
            {
                Sides.Array[i] = 2;
            }
            else if (IsNear(position.X, position2.X, position3.X, 0f))
            {
                Sides.Array[i] = 3;
            }
            else if (IsNear(position.Y, position2.Y, position3.Y, 1f))
            {
                Sides.Array[i] = 4;
            }
            else
            {
                Sides.Array[i] = IsNear(position.Y, position2.Y, position3.Y, 0f) ? (sbyte)5 : (sbyte)-1;
            }
        }
    }

    public void Trim()
    {
        Vertices.Capacity = Vertices.Count;
        Indices.Capacity = Indices.Count;
        Sides.Capacity = Sides.Count;
    }

    public static T[] GetVertexData<T>(VertexBuffer vertexBuffer)
    {
        if (vertexBuffer.Tag is not byte[] array)
        {
            throw new InvalidOperationException("VertexBuffer does not contain source data in Tag.");
        }

        if (array.Length % Utilities.SizeOf<T>() != 0)
        {
            throw new InvalidOperationException("VertexBuffer data size is not a whole multiply of target type size.");
        }

        var array2 = new T[array.Length / Utilities.SizeOf<T>()];
        var gCHandle = GCHandle.Alloc(array2, GCHandleType.Pinned);
        try
        {
            Marshal.Copy(array, 0, gCHandle.AddrOfPinnedObject(), Utilities.SizeOf<T>() * array2.Length);
            return array2;
        }
        finally
        {
            gCHandle.Free();
        }
    }

    public static T[] GetIndexData<T>(IndexBuffer indexBuffer)
    {
        if (indexBuffer.Tag is not byte[] array)
        {
            throw new InvalidOperationException("IndexBuffer does not contain source data in Tag.");
        }

        if (array.Length % Utilities.SizeOf<T>() != 0)
        {
            throw new InvalidOperationException("IndexBuffer data size is not a whole multiply of target type size.");
        }

        var array2 = new T[array.Length / Utilities.SizeOf<T>()];
        var gCHandle = GCHandle.Alloc(array2, GCHandleType.Pinned);
        try
        {
            Marshal.Copy(array, 0, gCHandle.AddrOfPinnedObject(), Utilities.SizeOf<T>() * array2.Length);
            return array2;
        }
        finally
        {
            gCHandle.Free();
        }
    }

    private static bool IsNear(float v1, float v2, float v3, float t)
    {
        if (v1 - t >= -0.001f && v1 - t <= 0.001f && v2 - t >= -0.001f && v2 - t <= 0.001f && v3 - t >= -0.001f)
        {
            return v3 - t <= 0.001f;
        }

        return false;
    }

    public void AppendImageExtrusion(Image image, Rectangle bounds, Vector3 scale, Color color, int alphaThreshold)
    {
        var count = Vertices.Count;
        AppendImageExtrusionSlice(image, bounds, new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
            new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, 0f), color, alphaThreshold);
        AppendImageExtrusionSlice(image, bounds, new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
            new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), color, alphaThreshold);
        for (var i = bounds.Left; i < bounds.Right; i++)
        {
            var image2 = new Image(1, bounds.Height);
            for (var j = bounds.Top; j < bounds.Bottom; j++)
            {
                if (i == bounds.Left || image.Pixels[i - 1 + j * image.Width].A <= alphaThreshold)
                {
                    image2.Pixels[j - bounds.Top] = image.Pixels[i + j * image.Width];
                }
            }

            AppendImageExtrusionSlice(image2, new Rectangle(0, 0, image2.Width, image2.Height), new Vector3(0f, 0f, 1f),
                new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f), new Vector3(i, bounds.Top, 0f), color,
                alphaThreshold);
        }

        for (var k = bounds.Left; k < bounds.Right; k++)
        {
            var image3 = new Image(1, bounds.Height);
            for (var l = bounds.Top; l < bounds.Bottom; l++)
            {
                if (k == bounds.Right - 1 || image.Pixels[k + 1 + l * image.Width].A <= alphaThreshold)
                {
                    image3.Pixels[l - bounds.Top] = image.Pixels[k + l * image.Width];
                }
            }

            AppendImageExtrusionSlice(image3, new Rectangle(0, 0, image3.Width, image3.Height), new Vector3(0f, 0f, 1f),
                new Vector3(0f, 1f, 0f), new Vector3(-1f, 0f, 0f), new Vector3(k + 1, bounds.Top, 0f), color,
                alphaThreshold);
        }

        for (var m = bounds.Top; m < bounds.Bottom; m++)
        {
            var image4 = new Image(bounds.Width, 1);
            for (var n = bounds.Left; n < bounds.Right; n++)
            {
                if (m == bounds.Top || image.Pixels[n + (m - 1) * image.Width].A <= alphaThreshold)
                {
                    image4.Pixels[n - bounds.Left] = image.Pixels[n + m * image.Width];
                }
            }

            AppendImageExtrusionSlice(image4, new Rectangle(0, 0, image4.Width, image4.Height), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f), new Vector3(bounds.Left, m, 0f), color,
                alphaThreshold);
        }

        for (var num = bounds.Top; num < bounds.Bottom; num++)
        {
            var image5 = new Image(bounds.Width, 1);
            for (var num2 = bounds.Left; num2 < bounds.Right; num2++)
            {
                if (num == bounds.Bottom - 1 || image.Pixels[num2 + (num + 1) * image.Width].A <= alphaThreshold)
                {
                    image5.Pixels[num2 - bounds.Left] = image.Pixels[num2 + num * image.Width];
                }
            }

            AppendImageExtrusionSlice(image5, new Rectangle(0, 0, image5.Width, image5.Height), new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f), new Vector3(0f, -1f, 0f), new Vector3(bounds.Left, num + 1, 0f), color,
                alphaThreshold);
        }

        for (var num3 = count; num3 < Vertices.Count; num3++)
        {
            Vertices.Array[num3].Position.X -= (bounds.Left + bounds.Right) / 2f;
            Vertices.Array[num3].Position.Y -= (bounds.Top + bounds.Bottom) / 2f;
            Vertices.Array[num3].Position.Z -= 0.5f;
            Vertices.Array[num3].Position.X *= scale.X;
            Vertices.Array[num3].Position.Y *= 0f - scale.Y;
            Vertices.Array[num3].Position.Z *= scale.Z;
            Vertices.Array[num3].TextureCoordinates.X /= image.Width;
            Vertices.Array[num3].TextureCoordinates.Y /= image.Height;
            Vertices.Array[num3].Color *= color;
        }
    }

    private void AppendImageExtrusionSlice(Image slice, Rectangle bounds, Vector3 right, Vector3 up, Vector3 forward,
        Vector3 position, Color color, int alphaThreshold)
    {
        var num = int.MaxValue;
        var num2 = int.MaxValue;
        var num3 = int.MinValue;
        var num4 = int.MinValue;
        for (var i = bounds.Top; i < bounds.Bottom; i++)
        for (var j = bounds.Left; j < bounds.Right; j++)
        {
            if (slice.Pixels[j + i * slice.Width].A > alphaThreshold)
            {
                num = MathUtils.Min(num, j);
                num2 = MathUtils.Min(num2, i);
                num3 = MathUtils.Max(num3, j);
                num4 = MathUtils.Max(num4, i);
            }
        }

        if (num != int.MaxValue)
        {
            var m = new Matrix(right.X, right.Y, right.Z, 0f, up.X, up.Y, up.Z, 0f, forward.X, forward.Y, forward.Z, 0f,
                position.X, position.Y, position.Z, 1f);
            var flip = m.Determinant() > 0f;
            var s = LightingManager.CalculateLighting(-forward);
            var p = Vector3.Transform(new Vector3(num, num2, 0f), m);
            var p2 = Vector3.Transform(new Vector3(num3 + 1, num2, 0f), m);
            var p3 = Vector3.Transform(new Vector3(num, num4 + 1, 0f), m);
            var p4 = Vector3.Transform(new Vector3(num3 + 1, num4 + 1, 0f), m);
            AppendImageExtrusionRectangle(p, p2, p3, p4, forward, flip, Color.MultiplyColorOnly(color, s));
        }
    }

    private void AppendImageExtrusionRectangle(Vector3 p11, Vector3 p21, Vector3 p12, Vector3 p22, Vector3 forward,
        bool flip, Color color)
    {
        var count = Vertices.Count;
        Vertices.Count += 4;
        var vertices = Vertices;
        var index = Vertices.Count - 4;
        var value = new BlockMeshVertex
        {
            Position = p11,
            TextureCoordinates = p11.XY + forward.XY / 2f,
            Color = color
        };
        vertices[index] = value;
        var vertices2 = Vertices;
        var index2 = Vertices.Count - 3;
        value = new BlockMeshVertex
        {
            Position = p21,
            TextureCoordinates = p21.XY + forward.XY / 2f,
            Color = color
        };
        vertices2[index2] = value;
        var vertices3 = Vertices;
        var index3 = Vertices.Count - 2;
        value = new BlockMeshVertex
        {
            Position = p12,
            TextureCoordinates = p12.XY + forward.XY / 2f,
            Color = color
        };
        vertices3[index3] = value;
        var vertices4 = Vertices;
        var index4 = Vertices.Count - 1;
        value = new BlockMeshVertex
        {
            Position = p22,
            TextureCoordinates = p22.XY + forward.XY / 2f,
            Color = color
        };
        vertices4[index4] = value;
        Indices.Count += 6;
        Indices[^6] = (ushort)count;
        if (flip)
        {
            Indices[^5] = (ushort)(count + 2);
            Indices[^4] = (ushort)(count + 1);
            Indices[^3] = (ushort)(count + 2);
            Indices[^2] = (ushort)(count + 3);
            Indices[^1] = (ushort)(count + 1);
        }
        else
        {
            Indices[^5] = (ushort)(count + 1);
            Indices[^4] = (ushort)(count + 2);
            Indices[^3] = (ushort)(count + 2);
            Indices[^2] = (ushort)(count + 1);
            Indices[^1] = (ushort)(count + 3);
        }
    }

#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
    private struct InternalVertex
    {
        public Vector3 Position;

        public Vector3 Normal;

        public Vector2 TextureCoordinate;
    }
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
}
