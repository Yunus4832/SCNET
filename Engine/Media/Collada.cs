using System.Globalization;
using System.Xml;
using System.Xml.Linq;

using Engine.Core;
using Engine.Graphics;

namespace Engine.Media;

public static class Collada
{
    public static bool IsColladaStream(Stream stream)
    {
        var result = false;
        var position = stream.Position;
        try
        {
            var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true
            });
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element)
                {
                    if (xmlReader.LocalName == "COLLADA")
                    {
                        result = true;
                    }

                    break;
                }
            }
        }
        catch (XmlException)
        {
        }

        stream.Position = position;
        return result;
    }

    public static ModelData Load(Stream stream)
    {
        var modelData = new ModelData();
        var colladaRoot = new ColladaRoot(XElement.Load(stream));
        if (colladaRoot.Scene.VisualScene.Nodes.Count > 1)
        {
            var modelBoneData = new ModelBoneData();
            modelData.Bones.Add(modelBoneData);
            modelBoneData.ParentBoneIndex = -1;
            modelBoneData.Name = string.Empty;
            modelBoneData.Transform = Matrix.Identity;
            foreach (var node in colladaRoot.Scene.VisualScene.Nodes)
            {
                LoadNode(modelData, modelBoneData, node, Matrix.CreateScale(colladaRoot.Asset.Meter));
            }
        }
        else
        {
            foreach (var node2 in colladaRoot.Scene.VisualScene.Nodes)
            {
                LoadNode(modelData, null, node2, Matrix.CreateScale(colladaRoot.Asset.Meter));
            }
        }

        foreach (var buffer in modelData.Buffers)
        {
            IndexVertices(buffer.VertexDeclaration.VertexStride, buffer.Vertices, out buffer.Vertices,
                out buffer.Indices);
        }

        return modelData;
    }

    private static ModelBoneData LoadNode(ModelData data, ModelBoneData? parentBoneData, ColladaNode node,
        Matrix transform)
    {
        var modelBoneData = new ModelBoneData();
        data.Bones.Add(modelBoneData);
        modelBoneData.ParentBoneIndex = parentBoneData != null ? data.Bones.IndexOf(parentBoneData) : -1;
        modelBoneData.Name = node.Name;
        modelBoneData.Transform = node.Transform * transform;
        foreach (var node2 in node.Nodes)
        {
            LoadNode(data, modelBoneData, node2, Matrix.Identity);
        }

        foreach (var geometry in node.Geometries)
        {
            LoadGeometry(data, modelBoneData, geometry);
        }

        return modelBoneData;
    }

    private static ModelMeshData LoadGeometry(ModelData data, ModelBoneData parentBoneData, ColladaGeometry geometry)
    {
        var modelMeshData = new ModelMeshData();
        data.Meshes.Add(modelMeshData);
        modelMeshData.Name = parentBoneData.Name;
        modelMeshData.ParentBoneIndex = data.Bones.IndexOf(parentBoneData);
        var flag = false;
        foreach (var polygon in geometry.Mesh.Polygons)
        {
            var modelMeshPartData = LoadPolygons(data, polygon);
            modelMeshData.MeshParts.Add(modelMeshPartData);
            modelMeshData.BoundingBox =
                flag
                    ? BoundingBox.Union(modelMeshData.BoundingBox, modelMeshPartData.BoundingBox)
                    : modelMeshPartData.BoundingBox;
            flag = true;
        }

        return modelMeshData;
    }

    private static ModelMeshPartData LoadPolygons(ModelData data, ColladaPolygons polygons)
    {
        var modelMeshPartData = new ModelMeshPartData();
        var num = 0;
        var dictionary = new Dictionary<VertexElement, ColladaInput>();
        foreach (var input in polygons.Inputs)
        {
            var str = input.Set == 0 ? string.Empty : input.Set.ToString(CultureInfo.InvariantCulture);
            if (input.Semantic == "POSITION")
            {
                dictionary[new VertexElement(num, VertexElementFormat.Vector3, "POSITION" + str)] = input;
                num += 12;
            }
            else if (input.Semantic == "NORMAL")
            {
                dictionary[new VertexElement(num, VertexElementFormat.Vector3, "NORMAL" + str)] = input;
                num += 12;
            }
            else if (input.Semantic == "TEXCOORD")
            {
                dictionary[new VertexElement(num, VertexElementFormat.Vector2, "TEXCOORD" + str)] = input;
                num += 8;
            }
            else if (input.Semantic == "COLOR")
            {
                dictionary[new VertexElement(num, VertexElementFormat.NormalizedByte4, "COLOR" + str)] = input;
                num += 4;
            }
        }

        var vertexDeclaration = new VertexDeclaration(dictionary.Keys.ToArray());
        var modelBuffersData = data.Buffers.FirstOrDefault(vd => vd.VertexDeclaration == vertexDeclaration);
        if (modelBuffersData == null)
        {
            modelBuffersData = new ModelBuffersData
            {
                VertexDeclaration = vertexDeclaration
            };
            data.Buffers.Add(modelBuffersData);
        }

        modelMeshPartData.BuffersDataIndex = data.Buffers.IndexOf(modelBuffersData);
        var num2 = polygons.P.Count / polygons.Inputs.Count;
        var list = new List<int>();
        if (polygons.VCount.Count == 0)
        {
            var num3 = 0;
            for (var i = 0; i < num2 / 3; i++)
            {
                list.Add(num3);
                list.Add(num3 + 2);
                list.Add(num3 + 1);
                num3 += 3;
            }
        }
        else
        {
            var num4 = 0;
            using (var enumerator2 = polygons.VCount.GetEnumerator())
            {
                while (enumerator2.MoveNext())
                {
                    switch (enumerator2.Current)
                    {
                        case 3:
                            list.Add(num4);
                            list.Add(num4 + 2);
                            list.Add(num4 + 1);
                            num4 += 3;
                            break;
                        case 4:
                            list.Add(num4);
                            list.Add(num4 + 2);
                            list.Add(num4 + 1);
                            list.Add(num4 + 2);
                            list.Add(num4);
                            list.Add(num4 + 3);
                            num4 += 4;
                            break;
                        default:
                            throw new NotSupportedException(
                                "Collada polygons with less than 3 or more than 4 vertices are not supported.");
                    }
                }
            }
        }

        var vertexStride = modelBuffersData.VertexDeclaration.VertexStride;
        var num5 = modelBuffersData.Vertices.Length;
        modelBuffersData.Vertices = ExtendArray(modelBuffersData.Vertices, list.Count * vertexStride);
        using (var binaryWriter =
               new BinaryWriter(new MemoryStream(modelBuffersData.Vertices, num5, list.Count * vertexStride)))
        {
            var flag = false;
            foreach (var item in dictionary)
            {
                var key = item.Key;
                var value = item.Value;
                if (key.Semantic.StartsWith("POSITION"))
                {
                    for (var j = 0; j < list.Count; j++)
                    {
                        var array = value.Source.Accessor.Source.Array;
                        var offset = value.Source.Accessor.Offset;
                        var stride = value.Source.Accessor.Stride;
                        var num6 = polygons.P[list[j] * polygons.Inputs.Count + value.Offset];
                        binaryWriter.BaseStream.Position = j * vertexStride + key.Offset;
                        var num7 = array[offset + stride * num6];
                        var num8 = array[offset + stride * num6 + 1];
                        var num9 = array[offset + stride * num6 + 2];
                        modelMeshPartData.BoundingBox = flag
                            ? BoundingBox.Union(modelMeshPartData.BoundingBox, new Vector3(num7, num8, num9))
                            : new BoundingBox(num7, num8, num9, num7, num8, num9);
                        flag = true;
                        binaryWriter.Write(num7);
                        binaryWriter.Write(num8);
                        binaryWriter.Write(num9);
                    }
                }
                else if (key.Semantic.StartsWith("NORMAL"))
                {
                    for (var k = 0; k < list.Count; k++)
                    {
                        var array2 = value.Source.Accessor.Source.Array;
                        var offset2 = value.Source.Accessor.Offset;
                        var stride2 = value.Source.Accessor.Stride;
                        var num10 = polygons.P[list[k] * polygons.Inputs.Count + value.Offset];
                        binaryWriter.BaseStream.Position = k * vertexStride + key.Offset;
                        var num11 = array2[offset2 + stride2 * num10];
                        var num12 = array2[offset2 + stride2 * num10 + 1];
                        var num13 = array2[offset2 + stride2 * num10 + 2];
                        var num14 = 1f / MathUtils.Sqrt(num11 * num11 + num12 * num12 + num13 * num13);
                        binaryWriter.Write(num14 * num11);
                        binaryWriter.Write(num14 * num12);
                        binaryWriter.Write(num14 * num13);
                    }
                }
                else if (key.Semantic.StartsWith("TEXCOORD"))
                {
                    for (var l = 0; l < list.Count; l++)
                    {
                        var array3 = value.Source.Accessor.Source.Array;
                        var offset3 = value.Source.Accessor.Offset;
                        var stride3 = value.Source.Accessor.Stride;
                        var num15 = polygons.P[list[l] * polygons.Inputs.Count + value.Offset];
                        binaryWriter.BaseStream.Position = l * vertexStride + key.Offset;
                        binaryWriter.Write(array3[offset3 + stride3 * num15]);
                        binaryWriter.Write(1f - array3[offset3 + stride3 * num15 + 1]);
                    }
                }
                else
                {
                    if (!key.Semantic.StartsWith("COLOR"))
                    {
                        throw new Exception();
                    }

                    for (var m = 0; m < list.Count; m++)
                    {
                        var array4 = value.Source.Accessor.Source.Array;
                        var offset4 = value.Source.Accessor.Offset;
                        var stride4 = value.Source.Accessor.Stride;
                        var num16 = polygons.P[list[m] * polygons.Inputs.Count + value.Offset];
                        binaryWriter.BaseStream.Position = m * vertexStride + key.Offset;
                        var color = new Color(array4[offset4 + stride4 * num16], array4[offset4 + stride4 * num16 + 1],
                            array4[offset4 + stride4 * num16 + 2], array4[offset4 + stride4 * num16 + 3]);
                        binaryWriter.Write(color.PackedValue);
                    }
                }
            }
        }

        modelMeshPartData.StartIndex = num5 / vertexStride;
        modelMeshPartData.IndicesCount = list.Count;
        return modelMeshPartData;
    }

    private static T[] ExtendArray<T>(T[] array, int extensionLength)
    {
        var array2 = new T[array.Length + extensionLength];
        Array.Copy(array, array2, array.Length);
        return array2;
    }

    private static void IndexVertices(int vertexStride, byte[] vertices, out byte[] resultVertices,
        out byte[] resultIndices)
    {
        var num = vertices.Length / vertexStride;
        var dictionary = new Dictionary<Vertex, ushort>();
        resultIndices = new byte[2 * num];
        for (var i = 0; i < num; i++)
        {
            var key = new Vertex(vertices, i * vertexStride, vertexStride);
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = (ushort)dictionary.Count;
                dictionary.Add(key, value);
            }

            resultIndices[i * 2] = (byte)value;
            resultIndices[i * 2 + 1] = (byte)(value >> 8);
        }

        resultVertices = new byte[dictionary.Count * vertexStride];
        foreach (var item in dictionary)
        {
            var key2 = item.Key;
            var value2 = item.Value;
            Array.Copy(key2.Data, key2.Start, resultVertices, value2 * vertexStride, key2.Count);
        }
    }

    private struct Vertex : IEquatable<Vertex>
    {
        public readonly byte[] Data;

        public readonly int Start;

        public readonly int Count;

        private readonly int _mHashCode;

        public Vertex(byte[] data, int start, int count)
        {
            Data = data;
            Start = start;
            Count = count;
            _mHashCode = 0;
            for (var i = 0; i < Count; i++)
            {
                _mHashCode += (7919 * i + 977) * Data[i + Start];
            }
        }

        public bool Equals(Vertex other)
        {
            if (_mHashCode != other._mHashCode || Data.Length != other.Data.Length)
            {
                return false;
            }

            for (var i = 0; i < Count; i++)
            {
                if (Data[i + Start] != other.Data[i + other.Start])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj)
        {
            return obj is Color color && Equals(color);
        }

        public override int GetHashCode()
        {
            return _mHashCode;
        }
    }

    private class Asset
    {
        public readonly float Meter = 1f;

        public Asset(XElement node)
        {
            var xElement = node.Element(ColladaRoot.Namespace + "unit");
            var xAttribute = xElement?.Attribute("meter");
            if (xAttribute != null)
            {
                Meter = float.Parse(xAttribute.Value, CultureInfo.InvariantCulture);
            }
        }
    }

    private class ColladaRoot
    {
        public static readonly XNamespace Namespace = "http://www.collada.org/2005/11/COLLADASchema";

        public readonly Asset Asset;

        public readonly List<ColladaLibraryGeometries> LibraryGeometries = [];

        public readonly List<ColladaLibraryVisualScenes> LibraryVisualScenes = [];

        public readonly Dictionary<string, ColladaNameId> ObjectsById = new();

        public readonly ColladaScene Scene;

        public ColladaRoot(XElement node)
        {
            Asset = new Asset(
                node.Element(Namespace + "asset") ?? throw new InvalidOperationException("asset element not found")
            );
            foreach (var item in node.Elements(Namespace + "library_geometries"))
            {
                LibraryGeometries.Add(new ColladaLibraryGeometries(this, item));
            }

            foreach (var item2 in node.Elements(Namespace + "library_visual_scenes"))
            {
                LibraryVisualScenes.Add(new ColladaLibraryVisualScenes(this, item2));
            }

            Scene = new ColladaScene(this,
                node.Element(Namespace + "scene") ?? throw new InvalidOperationException("scene element not found")
            );
        }
    }

    private class ColladaNameId
    {
        public readonly string Id = string.Empty;

        public readonly string Name = string.Empty;

        public ColladaNameId(ColladaRoot collada, XElement node, string idPostfix = "")
        {
            var xAttribute = node.Attribute("id");
            if (xAttribute != null)
            {
                Id = xAttribute.Value + idPostfix;
                collada.ObjectsById.Add(Id, this);
            }

            var xAttribute2 = node.Attribute("name");
            if (xAttribute2 != null)
            {
                Name = xAttribute2.Value;
            }
        }
    }

    private class ColladaLibraryVisualScenes
    {
        public readonly List<ColladaVisualScene> VisualScenes = [];

        public ColladaLibraryVisualScenes(ColladaRoot collada, XElement node)
        {
            foreach (var item in node.Elements(ColladaRoot.Namespace + "visual_scene"))
            {
                VisualScenes.Add(new ColladaVisualScene(collada, item));
            }
        }
    }

    private class ColladaLibraryGeometries
    {
        public readonly List<ColladaGeometry> Geometries = [];

        public ColladaLibraryGeometries(ColladaRoot collada, XElement node)
        {
            foreach (var item in node.Elements(ColladaRoot.Namespace + "geometry"))
            {
                Geometries.Add(new ColladaGeometry(collada, item));
            }
        }
    }

    private class ColladaScene
    {
        public readonly ColladaVisualScene VisualScene;

        public ColladaScene(ColladaRoot collada, XElement node)
        {
            var xElement = node.Element(ColladaRoot.Namespace + "instance_visual_scene");
            var urlPart = xElement?.Attribute("url")?.Value[1..];
            if (urlPart is null)
            {
                throw new InvalidOperationException("url attribute not found");
            }

            var url = urlPart + "-ColladaVisualScene";
            VisualScene = (ColladaVisualScene)collada.ObjectsById[url];
        }
    }

    private class ColladaVisualScene : ColladaNameId
    {
        public readonly List<ColladaNode> Nodes = [];

        public ColladaVisualScene(ColladaRoot collada, XElement node)
            : base(collada, node, "-ColladaVisualScene")
        {
            foreach (var item in node.Elements(ColladaRoot.Namespace + "node"))
            {
                Nodes.Add(new ColladaNode(collada, item));
            }
        }
    }

    private class ColladaNode : ColladaNameId
    {
        public readonly List<ColladaGeometry> Geometries = new();

        public readonly List<ColladaNode> Nodes = new();
        public readonly Matrix Transform = Matrix.Identity;

        public ColladaNode(ColladaRoot collada, XElement node)
            : base(collada, node)
        {
            foreach (var item in node.Elements())
            {
                if (item.Name == ColladaRoot.Namespace + "matrix")
                {
                    var array = (from s in item.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                        select float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    Transform = Matrix.Transpose(new Matrix(array[0], array[1], array[2], array[3], array[4], array[5],
                        array[6], array[7], array[8], array[9], array[10], array[11], array[12], array[13], array[14],
                        array[15])) * Transform;
                }
                else if (item.Name == ColladaRoot.Namespace + "translate")
                {
                    var array2 = (from s in item.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                        select float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    Transform = Matrix.CreateTranslation(array2[0], array2[1], array2[2]) * Transform;
                }
                else if (item.Name == ColladaRoot.Namespace + "rotate")
                {
                    var array3 = (from s in item.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                        select float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    Transform = Matrix.CreateFromAxisAngle(new Vector3(array3[0], array3[1], array3[2]),
                        MathUtils.DegToRad(array3[3])) * Transform;
                }
                else if (item.Name == ColladaRoot.Namespace + "scale")
                {
                    var array4 = (from s in item.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                        select float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                    Transform = Matrix.CreateScale(array4[0], array4[1], array4[2]) * Transform;
                }
            }

            foreach (var item2 in node.Elements(ColladaRoot.Namespace + "node"))
            {
                Nodes.Add(new ColladaNode(collada, item2));
            }

            foreach (var item3 in node.Elements(ColladaRoot.Namespace + "instance_geometry"))
            {
                var url = item3.Attribute("url")?.Value[1..] ??
                          throw new InvalidOperationException("url attribute not found");
                Geometries.Add((ColladaGeometry)collada.ObjectsById[url]);
            }
        }
    }

    private class ColladaGeometry : ColladaNameId
    {
        public readonly ColladaMesh Mesh;

        public ColladaGeometry(ColladaRoot collada, XElement node)
            : base(collada, node)
        {
            var xElement = node.Element(ColladaRoot.Namespace + "mesh") ??
                           throw new InvalidOperationException("mesh element not found");
            Mesh = new ColladaMesh(collada, xElement);
        }
    }

    private class ColladaMesh
    {
        public readonly List<ColladaPolygons> Polygons = [];
        public readonly List<ColladaSource> Sources = [];

        public ColladaVertices Vertices;

        public ColladaMesh(ColladaRoot collada, XElement node)
        {
            foreach (var item in node.Elements(ColladaRoot.Namespace + "source"))
            {
                Sources.Add(new ColladaSource(collada, item));
            }

            var node2 = node.Element(ColladaRoot.Namespace + "vertices") ??
                        throw new InvalidOperationException("vertices element not found");
            Vertices = new ColladaVertices(collada, node2);
            foreach (var item2 in node.Elements(ColladaRoot.Namespace + "polygons")
                         .Concat(node.Elements(ColladaRoot.Namespace + "polylist"))
                         .Concat(node.Elements(ColladaRoot.Namespace + "triangles")))
            {
                Polygons.Add(new ColladaPolygons(collada, item2));
            }
        }
    }

    private class ColladaSource : ColladaNameId
    {
        public readonly ColladaAccessor Accessor;
        public ColladaFloatArray FloatArray;

        public ColladaSource(ColladaRoot collada, XElement node)
            : base(collada, node)
        {
            var floatArray = node.Element(ColladaRoot.Namespace + "float_array") ??
                             throw new InvalidOperationException("float_array element not found");
            FloatArray = new ColladaFloatArray(collada, floatArray);
            var techniqueCommon = node.Element(ColladaRoot.Namespace + "technique_common") ??
                                  throw new InvalidOperationException("technique_common element not found");
            var accessor = techniqueCommon.Element(ColladaRoot.Namespace + "accessor") ??
                           throw new InvalidOperationException("accessor element not found");
            Accessor = new ColladaAccessor(collada, accessor);
        }
    }

    private class ColladaFloatArray : ColladaNameId
    {
        public readonly float[] Array;

        public ColladaFloatArray(ColladaRoot collada, XElement node)
            : base(collada, node)
        {
            Array = (from s in node.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                select float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        }
    }

    private class ColladaAccessor
    {
        public readonly int Offset;
        public readonly ColladaFloatArray Source;

        public readonly int Stride = 1;

        public ColladaAccessor(ColladaRoot collada, XElement node)
        {
            var source = node.Attribute("source")?.Value[1..];
            if (source is null)
            {
                throw new InvalidOperationException("source attribute not found");
            }

            Source = (ColladaFloatArray)collada.ObjectsById[source];
            var offset = node.Attribute("offset")?.Value ?? "0";
            Offset = int.Parse(offset, CultureInfo.InvariantCulture);
            var stride = node.Attribute("stride")?.Value ?? "1";
            Stride = int.Parse(stride, CultureInfo.InvariantCulture);
        }
    }

    private class ColladaVertices : ColladaNameId
    {
        public readonly string Semantic;

        public readonly ColladaSource Source;

        public ColladaVertices(ColladaRoot collada, XElement node)
            : base(collada, node)
        {
            var xElement = node.Element(ColladaRoot.Namespace + "input") ??
                           throw new InvalidOperationException("input element not found");
            var xAttribute = xElement.Attribute("semantic") ??
                             throw new InvalidOperationException("semantic attribute not found");
            var source = xElement.Attribute("source") ??
                         throw new InvalidOperationException("source attribute not found");
            Semantic = xAttribute.Value;
            Source = (ColladaSource)collada.ObjectsById[source.Value[1..]];
        }
    }

    private class ColladaPolygons
    {
        public readonly List<ColladaInput> Inputs = [];

        public readonly List<int> P = [];

        public readonly List<int> VCount = [];

        public ColladaPolygons(ColladaRoot collada, XElement node)
        {
            foreach (var item in node.Elements(ColladaRoot.Namespace + "input"))
            {
                Inputs.Add(new ColladaInput(collada, item));
            }

            foreach (var item2 in node.Elements(ColladaRoot.Namespace + "vcount"))
            {
                VCount.AddRange(from s in item2.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                    select int.Parse(s, CultureInfo.InvariantCulture));
            }

            foreach (var item3 in node.Elements(ColladaRoot.Namespace + "p"))
            {
                P.AddRange(from s in item3.Value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                    select int.Parse(s, CultureInfo.InvariantCulture));
            }
        }
    }

    private class ColladaInput
    {
        public readonly int Offset;

        public readonly string Semantic;

        public readonly int Set;

        public readonly ColladaSource Source;

        public ColladaInput(ColladaRoot collada, XElement node)
        {
            var offset = node.Attribute("offset")?.Value ?? "0";
            Offset = int.Parse(offset, CultureInfo.InvariantCulture);
            var set = node.Attribute("set")?.Value ?? "0";
            Set = int.Parse(set, CultureInfo.InvariantCulture);
            var semantic = node.Attribute("semantic") ??
                           throw new InvalidOperationException("semantic attribute not found");
            Semantic = semantic.Value;
            var source = node.Attribute("source") ??
                         throw new InvalidOperationException("source attribute not found");
            var colladaNameId = collada.ObjectsById[source.Value[1..]];
            if (colladaNameId is ColladaVertices colladaVertices)
            {
                Source = colladaVertices.Source;
                Semantic = colladaVertices.Semantic;
            }
            else
            {
                Source = (ColladaSource)colladaNameId;
            }
        }
    }
}
