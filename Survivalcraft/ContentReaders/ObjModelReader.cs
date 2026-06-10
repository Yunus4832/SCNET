using Engine.Graphics;

namespace Game.ContentReaders;

public class ObjModelReader : IContentReader
{
    private static readonly Dictionary<int, List<int>> _faceMap = new();

    static ObjModelReader()
    {
        _faceMap.Add(4, [0, 2, 1]); //顶面
        _faceMap.Add(5, [0, 2, 1]); //底面

        _faceMap.Add(2, [0, 2, 1]); //逆
        _faceMap.Add(3, [0, 2, 1]); //逆

        _faceMap.Add(0, [0, 2, 1]); //顺
        _faceMap.Add(1, [0, 2, 1]); //顺
    }

    public override string Type => "Game.ObjModel";

    public override string[] DefaultSuffix => ["obj"];

    public override object Get(ContentInfo[] contents)
    {
        return Load(contents[0].Duplicate());
    }

    public static ObjModel Load(Stream stream)
    {
        var meshes = new Dictionary<string, ObjMesh>();
        var texturePaths = new Dictionary<string, string>();
        var objPositions = new List<ObjPosition>();
        var objTexCoodList = new List<ObjTexCood>();
        var objNormals = new List<ObjNormal>();
        using (stream)
        {
            var streamReader = new StreamReader(stream);
            ObjMesh? objMesh = null;
            string? currentKey = null;
            while (!streamReader.EndOfStream)
            {
                var line = streamReader.ReadLine();
                if (line != null)
                {
                    var spl = line.Split([(char)0x09, (char)0x20], StringSplitOptions.None);
                    switch (spl[0])
                    {
                        case "mtllib":
                        {
                            var mtllibStruct = ContentManager.Get<MtllibStruct>(spl[1]);
                            texturePaths = mtllibStruct.TexturePaths;
                            break;
                        }
                        case "o":
                        {
                            if (meshes.TryGetValue(spl[1], out var mesh))
                            {
                                objMesh = mesh;
                            }
                            else
                            {
                                objMesh = new ObjMesh(spl[1]);
                                meshes.Add(spl[1], objMesh);
                            }

                            break;
                        }
                        case "v":
                        {
                            objPositions.Add(new ObjPosition(spl[1], spl[2], spl[3]));
                            break;
                        }
                        case "vt":
                        {
                            objTexCoodList.Add(new ObjTexCood(spl[1], spl[2]));
                            break;
                        }
                        case "vn":
                        {
                            objNormals.Add(new ObjNormal(spl[1], spl[2], spl[3]));
                            break;
                        }
                        case "usemtl":
                        {
                            if (texturePaths.TryGetValue(spl[1], out currentKey))
                            {
                                // LoadingScreen.Info("Parse Obj mtl:" + currentKey);
                            }

                            break;
                        }
                        case "f":
                        {
                            if (string.IsNullOrEmpty(currentKey))
                            {
                                currentKey = "Textures/NoneTexture";
                            }

                            objMesh!.TexturePath = currentKey;
                            var sideCount = spl.Length - 1;
                            if (sideCount != 3)
                            {
                                throw new Exception("模型必须为三角面");
                            }

                            var i = 0;
                            var startCount = objMesh.Vertices.Count;
                            while (++i < spl.Length)
                            {
                                var param = spl[i].Split(['/'], StringSplitOptions.None);
                                if (param.Length != 3)
                                {
                                    throw new Exception("面参数错误");
                                }

                                var pa = int.Parse(param[0]); // 顶点索引
                                var pb = int.Parse(param[1]); // 纹理索引
                                var pc = int.Parse(param[2]); // 法线索引
                                var objPosition = objPositions[pa - 1];
                                var texCood = objTexCoodList[pb - 1];
                                var objNormal = objNormals[pc - 1];
                                var face = CellFace.Vector3ToFace(new Vector3(objNormal.X, objNormal.Y, objNormal.Z));
                                objMesh.Indices.Add((ushort)(startCount + _faceMap[face][i - 1]));
                                objMesh.Vertices.Add(new ObjVertex
                                    { Position = objPosition, ObjNormal = objNormal, TexCood = texCood });
                            }

                            break;
                        }
                    }
                }
            }
        }

        return ObjMeshesToModel<ObjModel>(meshes);
    }

    private static void AppendMesh(Model model, ModelBone rootBone, string texturePath, ObjMesh objMesh)
    {
        var modelBone = model.NewBone(
            objMesh.MeshName,
            objMesh.MeshMatrix ?? Matrix.Identity,
            rootBone
        );
        if (objMesh.Vertices.Count > 0)
        {
            var mesh = model.NewMesh(objMesh.MeshName, modelBone, objMesh.CalculateBoundingBox());
            var vertexBuffer = new VertexBuffer(new VertexDeclaration(
                    new VertexElement(0, VertexElementFormat.Vector3, VertexElementSemantic.Position),
                    new VertexElement(12, VertexElementFormat.Vector3, VertexElementSemantic.Normal),
                    new VertexElement(24, VertexElementFormat.Vector2, VertexElementSemantic.TextureCoordinate)),
                objMesh.Vertices.Count);
            var stream1 = new MemoryStream();
            var stream2 = new MemoryStream();
            var binaryWriter1 = new BinaryWriter(stream1);
            var binaryWriter2 = new BinaryWriter(stream2);
            foreach (var objVertex in objMesh.Vertices)
            {
                binaryWriter1.Write(objVertex.Position.X);
                binaryWriter1.Write(objVertex.Position.Y);
                binaryWriter1.Write(objVertex.Position.Z);
                binaryWriter1.Write(objVertex.ObjNormal.X);
                binaryWriter1.Write(objVertex.ObjNormal.Y);
                binaryWriter1.Write(objVertex.ObjNormal.Z);
                binaryWriter1.Write(objVertex.TexCood.Tx);
                binaryWriter1.Write(objVertex.TexCood.Ty);
            }

            foreach (var index in objMesh.Indices)
            {
                binaryWriter2.Write(index);
            }

            var vs = stream1.ToArray();
            var ins = stream2.ToArray();
            stream1.Close();
            stream2.Close();
            vertexBuffer.SetData(objMesh.Vertices.Array, 0, objMesh.Vertices.Count);
            vertexBuffer.Tag = vs;
            var indexBuffer = new IndexBuffer(IndexFormat.SixteenBits, objMesh.Indices.Count);
            indexBuffer.SetData(objMesh.Indices.Array, 0, objMesh.Indices.Count);
            indexBuffer.Tag = ins;
            var modelMeshPart = mesh.NewMeshPart(vertexBuffer, indexBuffer, 0, objMesh.Indices.Count,
                objMesh.CalculateBoundingBox());
            modelMeshPart.TexturePath = objMesh.TexturePath;
            model.AddMesh(mesh);
        }

        foreach (var objMesh1 in objMesh.ChildMeshes)
        {
            AppendMesh(model, modelBone, objMesh1.TexturePath, objMesh1);
        }
    }

    public static T ObjMeshesToModel<T>(Dictionary<string, ObjMesh> meshes) where T : class
    {
        var cType = typeof(T);
        if (!cType.IsSubclassOf(typeof(Model)))
        {
            throw new Exception("不能将" + cType.Name + "转换为Model类型");
        }

        var obj = Activator.CreateInstance(cType);
        if (obj is not Model model)
        {
            return obj as T ?? throw new InvalidOperationException("Can not convert to required Type");
        }

        var rootBone = model.NewBone("Object", Matrix.Identity, null);
        foreach (var c in meshes)
        {
            AppendMesh(model, rootBone, c.Key, c.Value);
        }

        return obj as T ?? throw new InvalidOperationException("Can not convert to required Type");
    }

    public struct ObjPosition
    {
        public readonly float X;

        public readonly float Y;

        public readonly float Z;

        public ObjPosition(string x, string y, string z)
        {
            X = float.Parse(x);
            Y = float.Parse(y);
            Z = float.Parse(z);
        }

        public ObjPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct ObjVertex
    {
        public ObjPosition Position;
        public ObjNormal ObjNormal;
        public ObjTexCood TexCood;
    }

    public struct ObjNormal
    {
        public readonly float X;

        public readonly float Y;

        public readonly float Z;

        public ObjNormal(string x, string y, string z)
        {
            X = float.Parse(x);
            Y = float.Parse(y);
            Z = float.Parse(z);
        }

        public ObjNormal(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct ObjTexCood
    {
        public readonly float Tx;

        public readonly float Ty;

        public ObjTexCood(string tx, string ty)
        {
            Tx = float.Parse(tx);
            Ty = float.Parse(ty);
        }

        public ObjTexCood(float tx, float ty)
        {
            Tx = tx;
            Ty = ty;
        }
    }

    public class ObjMesh(string meshName)
    {
        public readonly List<ObjMesh> ChildMeshes = [];

        public int ElementIndex;

        public readonly DynamicArray<ushort> Indices = [];

        public Matrix? MeshMatrix;

        public readonly string MeshName = meshName;

        public string TexturePath = "Textures/NoneTexture"; // 默认位置

        public readonly DynamicArray<ObjVertex> Vertices = [];

        public BoundingBox CalculateBoundingBox()
        {
            var vectors = new List<Vector3>();
            for (var i = 0; i < Vertices.Count; i++)
            {
                vectors.Add(new Vector3(Vertices[i].Position.X, Vertices[i].Position.Y, Vertices[i].Position.Z));
            }

            return new BoundingBox(vectors);
        }
    }
}
