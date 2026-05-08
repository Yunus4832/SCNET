using System.Text.Json;
using System.Text.Json.Nodes;

namespace Game.ModManager;

/**
 * 此处基础坐标系为YZX
 */
public class JsonModelReader
{
    public static readonly Dictionary<string, List<Vector3>> FacesDic = new();

    public static readonly Dictionary<string, Vector3> NormalDic = new();

    public static readonly Dictionary<string, List<int>> FacedirecDic = new();

    public static readonly Dictionary<float, List<int>> TextureRotate = new();

    static JsonModelReader()
    {
        FacesDic.Add("north", [Vector3.UnitX, Vector3.Zero, Vector3.UnitY, new Vector3(1, 1, 0)]);
        FacesDic.Add("south", [new Vector3(1, 0, 1), Vector3.UnitZ, new Vector3(0, 1, 1), new Vector3(1, 1, 1)]);

        FacesDic.Add("east", [new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0)]);
        FacesDic.Add("west", [Vector3.Zero, Vector3.UnitZ, new Vector3(0, 1, 1), Vector3.UnitY]);

        FacesDic.Add("up", [Vector3.UnitY, new Vector3(0, 1, 1), Vector3.One, new Vector3(1, 1, 0)]);
        FacesDic.Add("down", [Vector3.Zero, Vector3.UnitZ, new Vector3(1, 0, 1), new Vector3(1, 0, 0)]);

        NormalDic.Add("north", new Vector3(0, 0, -1));
        NormalDic.Add("south", new Vector3(0, 0, 1));
        NormalDic.Add("east", new Vector3(1, 0, 0));
        NormalDic.Add("west", new Vector3(-1, 0, 0));
        NormalDic.Add("up", new Vector3(0, 1, 0));
        NormalDic.Add("down", new Vector3(0, -1, 0));

        FacedirecDic.Add("north", [0, 2, 1, 0, 3, 2]); //逆
        FacedirecDic.Add("west", [0, 2, 1, 0, 3, 2]); //逆
        FacedirecDic.Add("up", [0, 2, 1, 0, 3, 2]); //逆

        FacedirecDic.Add("south", [0, 1, 2, 0, 2, 3]); //顺
        FacedirecDic.Add("east", [0, 1, 2, 0, 2, 3]); //顺
        FacedirecDic.Add("down", [0, 1, 2, 0, 2, 3]); //顺

        TextureRotate.Add(0f, [0, 3, 2, 3, 2, 1, 0, 1]);
        TextureRotate.Add(90f, [0, 1, 0, 3, 2, 3, 2, 1]);
        TextureRotate.Add(180f, [2, 1, 0, 1, 0, 3, 2, 3]);
        TextureRotate.Add(270f, [2, 3, 2, 1, 0, 1, 0, 3]);
    }

    private static float ObjConvertFloat(object obj)
    {
        if (obj is double d)
        {
            return (float)d;
        }

        if (obj is long l)
        {
            return l;
        }

        throw new Exception("错误的数据转换，不能将" + obj.GetType().Name + "转换为float");
    }

    public static JsonModel Load(Stream stream)
    {
        var meshes = new Dictionary<string, ObjModelReader.ObjMesh>();
        var firstPersonOffset = Vector3.One;
        var firstPersonRotation = Vector3.Zero;
        var firstPersonScale = Vector3.One;
        var inHandOffset = Vector3.One;
        var inHandRotation = Vector3.Zero;
        var inHandScale = Vector3.One;
        var parent = string.Empty;
        using (stream)
        {
            var modelObj = JsonSerializer.Deserialize<JsonObject>(new StreamReader(stream).ReadToEnd());
            if (modelObj is not null)
            {
                var textureSize = Vector2.Zero;
                var textureDict = new Dictionary<string, string>();
                if (modelObj.TryGetPropertyValue("display", out var displayNode))
                {
                    if (displayNode is JsonObject displayObj)
                    {
                        if (displayObj.TryGetPropertyValue("thirdperson_righthand", out var thirdpersonRighthandNode))
                        {
                            if (thirdpersonRighthandNode is JsonObject thirdpersonRighthandObj)
                            {
                                if (thirdpersonRighthandObj.TryGetPropertyValue("rotation", out var rotationNode))
                                {
                                    if (rotationNode is JsonArray rotationArray)
                                    {
                                        inHandRotation = new Vector3(ObjConvertFloat(rotationArray[0]!),
                                            ObjConvertFloat(rotationArray[1]!),
                                            ObjConvertFloat(rotationArray[2]!)
                                        );
                                    }
                                }

                                if (thirdpersonRighthandObj.TryGetPropertyValue("translation", out var translationNode))
                                {
                                    if (translationNode is JsonArray tranlationArray)
                                    {
                                        inHandOffset = new Vector3(ObjConvertFloat(tranlationArray[0]!),
                                            ObjConvertFloat(tranlationArray[1]!),
                                            ObjConvertFloat(tranlationArray[2]!)
                                        );
                                    }
                                }

                                if (thirdpersonRighthandObj.TryGetPropertyValue("scale", out var scaleNode))
                                {
                                    if (scaleNode is JsonArray scaleArray)
                                    {
                                        inHandScale = new Vector3(ObjConvertFloat(scaleArray[0]!),
                                            ObjConvertFloat(scaleArray[1]!),
                                            ObjConvertFloat(scaleArray[2]!)
                                        );
                                    }
                                }
                            }
                        }

                        if (displayObj.TryGetPropertyValue("firstperson_righthand", out var firstpersonRighthandNode))
                        {
                            if (firstpersonRighthandNode is JsonObject firstpersonRighthandObj)
                            {
                                if (firstpersonRighthandObj.TryGetPropertyValue("rotation", out var rotationNode))
                                {
                                    if (rotationNode is JsonArray rotationArray)
                                    {
                                        firstPersonRotation = new Vector3(ObjConvertFloat(rotationArray[0]!),
                                            ObjConvertFloat(rotationArray[1]!), ObjConvertFloat(rotationArray[2]!));
                                    }
                                }

                                if (firstpersonRighthandObj.TryGetPropertyValue("translation", out var translationNode))
                                {
                                    if (translationNode is JsonArray translationArray)
                                    {
                                        firstPersonOffset = new Vector3(ObjConvertFloat(translationArray[0]!),
                                            ObjConvertFloat(translationArray[1]!),
                                            ObjConvertFloat(translationArray[2]!));
                                    }
                                }

                                if (firstpersonRighthandObj.TryGetPropertyValue("scale", out var scaleNode))
                                {
                                    if (scaleNode is JsonArray scaleArray)
                                    {
                                        firstPersonScale = new Vector3(ObjConvertFloat(scaleArray[0]!),
                                            ObjConvertFloat(scaleArray[1]!),
                                            ObjConvertFloat(scaleArray[2]!));
                                    }
                                }
                            }
                        }
                    }
                }

                modelObj.TryGetPropertyValue("parent", out var parentNode);
                parent = parentNode?.ToString();
                if (modelObj.TryGetPropertyValue("textures", out var texturesNode))
                {
                    if (texturesNode is JsonObject texturesObj)
                    {
                        foreach (var item in texturesObj)
                        {
                            textureDict.Add(item.Key, item.Value?.ToString() ?? string.Empty);
                        }
                    }
                }

                if (modelObj.TryGetPropertyValue("texture_size", out var textureSizeNode))
                {
                    if (textureSizeNode is JsonArray textureSizeArray)
                    {
                        textureSize = new Vector2(
                            ObjConvertFloat(textureSizeArray[0]!),
                            ObjConvertFloat(textureSizeArray[1]!)
                        );
                    }
                }

                if (modelObj.TryGetPropertyValue("elements", out var elementNode))
                {
                    if (elementNode is JsonArray elementArray)
                    {
                        for (var l = 0; l < elementArray.Count; l++)
                        {
                            if (elementArray[l] is not JsonObject elementObj)
                            {
                                continue;
                            }

                            var fromArray = (elementObj["from"] as JsonArray)!;
                            var toArray = (elementObj["to"] as JsonArray)!;
                            elementObj.TryGetPropertyValue("name", out var elementName);
                            var name = elementName?.ToString() ?? "undefined";
                            if (!meshes.TryGetValue(name, out var objMesh))
                            {
                                objMesh = new ObjModelReader.ObjMesh(name)
                                {
                                    ElementIndex = l
                                };
                                meshes.Add(name, objMesh);
                            }

                            if (elementObj.TryGetPropertyValue("rotation", out var rotationNode))
                                //处理模型旋转
                            {
                                if (rotationNode is JsonObject rotationObj)
                                {
                                    var origin = rotationObj["origin"] as JsonArray;
                                    var angle = ObjConvertFloat(rotationObj["angle"]!);
                                }
                            }

                            var start = new Vector3(ObjConvertFloat(fromArray[0]!), ObjConvertFloat(fromArray[1]!),
                                ObjConvertFloat(fromArray[2]!));
                            var end = new Vector3(ObjConvertFloat(toArray[0]!), ObjConvertFloat(toArray[1]!),
                                ObjConvertFloat(toArray[2]!));
                            var transform = Matrix.CreateScale(end.X - start.X, end.Y - start.Y, end.Z - start.Z) *
                                            Matrix.CreateTranslation(start.X, start.Y, start.Z) *
                                            Matrix.CreateScale(0.0625f); //基础缩放变换
                            if (elementObj.TryGetPropertyValue("faces", out var facesNode))
                            {
                                //每个面，开始生成六个面的顶点数据
                                var facesObj = (facesNode as JsonObject)!;
                                foreach (var (faceName, value) in facesObj)
                                {
                                    var childMesh = new ObjModelReader.ObjMesh(faceName);
                                    var vectors = FacesDic[faceName]; //预取出四个面的点
                                    var faceObj = (value as JsonObject)!;
                                    var uvs = new float[4];
                                    var texCoords = new List<Vector2>();
                                    var rotate = 0f;
                                    if (faceObj.TryGetPropertyValue("rotation", out var rotationNode2))
                                        //处理uv旋转数据
                                    {
                                        rotate = ObjConvertFloat(rotationNode2!);
                                    }

                                    if (faceObj.TryGetPropertyValue("uv", out var uvNode))
                                    {
                                        //处理uv坐标数据
                                        var uvArray = (uvNode as JsonArray)!;
                                        for (var k = 0; k < uvArray.Count; k++)
                                        {
                                            uvs[k] = ObjConvertFloat(uvArray[k]!) / 16f;
                                        }

                                        var center = new Vector2(uvs[2] - uvs[0], uvs[3] - uvs[1]) / 2f +
                                                     new Vector2(uvs[0], uvs[1]); //中心点
                                        texCoords.Add(new Vector2(uvs[TextureRotate[rotate][0]],
                                            uvs[TextureRotate[rotate][1]])); //x1,y2
                                        texCoords.Add(new Vector2(uvs[TextureRotate[rotate][2]],
                                            uvs[TextureRotate[rotate][3]])); //x1,y2
                                        texCoords.Add(new Vector2(uvs[TextureRotate[rotate][4]],
                                            uvs[TextureRotate[rotate][5]])); //x1,y2
                                        texCoords.Add(new Vector2(uvs[TextureRotate[rotate][6]],
                                            uvs[TextureRotate[rotate][7]])); //x1,y2
                                    }

                                    if (faceObj.TryGetPropertyValue("texture", out var textureNode))
                                    {
                                        //处理贴图数据
                                        var tkey = textureNode!.ToString(); // 面名字
                                        if (textureDict.TryGetValue(tkey[1..], out var path))
                                        {
                                            childMesh.TexturePath = path;
                                        }
                                    }

                                    var ops = new ObjModelReader.ObjPosition[3];
                                    var ots = new ObjModelReader.ObjTexCood[3];
                                    var ons = new ObjModelReader.ObjNormal[3];
                                    //生成第一个三角面顶点
                                    var c1 = FacedirecDic[faceName][0];
                                    var c2 = FacedirecDic[faceName][1];
                                    var c3 = FacedirecDic[faceName][2];
                                    var p1 = Vector3.Transform(vectors[c1], transform);
                                    var p2 = Vector3.Transform(vectors[c2], transform);
                                    var p3 = Vector3.Transform(vectors[c3], transform);
                                    ops[0] = new ObjModelReader.ObjPosition(p1.X, p1.Y, p1.Z);
                                    ops[1] = new ObjModelReader.ObjPosition(p2.X, p2.Y, p2.Z);
                                    ops[2] = new ObjModelReader.ObjPosition(p3.X, p3.Y, p3.Z);
                                    //生成第一个三角面的纹理坐标
                                    var t1 = texCoords[c1];
                                    var t2 = texCoords[c2];
                                    var t3 = texCoords[c3];
                                    ots[0] = new ObjModelReader.ObjTexCood(t1.X, t1.Y);
                                    ots[1] = new ObjModelReader.ObjTexCood(t2.X, t2.Y);
                                    ots[2] = new ObjModelReader.ObjTexCood(t3.X, t3.Y);
                                    //生成第一个三角面的顶点法线
                                    //Vector3 normal = NormalDic[facename];
                                    var startcount = childMesh.Vertices.Count;
                                    childMesh.Indices.Add((ushort)startcount++);
                                    childMesh.Indices.Add((ushort)startcount++);
                                    childMesh.Indices.Add((ushort)startcount++);
                                    childMesh.Vertices.Add(new ObjModelReader.ObjVertex
                                    {
                                        Position = ops[0], ObjNormal = new ObjModelReader.ObjNormal(0, 0, 0),
                                        TexCood = ots[0]
                                    });
                                    childMesh.Vertices.Add(new ObjModelReader.ObjVertex
                                    {
                                        Position = ops[1], ObjNormal = new ObjModelReader.ObjNormal(0, 0, 0),
                                        TexCood = ots[1]
                                    });
                                    childMesh.Vertices.Add(new ObjModelReader.ObjVertex
                                    {
                                        Position = ops[2], ObjNormal = new ObjModelReader.ObjNormal(0, 0, 0),
                                        TexCood = ots[2]
                                    });
                                    //生成第二个三角面
                                    c1 = FacedirecDic[faceName][3];
                                    c2 = FacedirecDic[faceName][4];
                                    c3 = FacedirecDic[faceName][5];
                                    p1 = Vector3.Transform(vectors[c1], transform);
                                    p2 = Vector3.Transform(vectors[c2], transform);
                                    p3 = Vector3.Transform(vectors[c3], transform);
                                    ops[0] = new ObjModelReader.ObjPosition(p1.X, p1.Y, p1.Z);
                                    ops[1] = new ObjModelReader.ObjPosition(p2.X, p2.Y, p2.Z);
                                    ops[2] = new ObjModelReader.ObjPosition(p3.X, p3.Y, p3.Z);
                                    //生成第二个三角面的纹理坐标
                                    t1 = texCoords[c1];
                                    t2 = texCoords[c2];
                                    t3 = texCoords[c3];
                                    ots[0] = new ObjModelReader.ObjTexCood(t1.X, t1.Y);
                                    ots[1] = new ObjModelReader.ObjTexCood(t2.X, t2.Y);
                                    ots[2] = new ObjModelReader.ObjTexCood(t3.X, t3.Y);
                                    //生成第二个三角面的顶点法线
                                    childMesh.Indices.Add((ushort)startcount++);
                                    childMesh.Indices.Add((ushort)startcount++);
                                    childMesh.Indices.Add((ushort)startcount++);
                                    childMesh.Vertices.Add(new ObjModelReader.ObjVertex
                                    {
                                        Position = ops[0], ObjNormal = new ObjModelReader.ObjNormal(0, 0, 0),
                                        TexCood = ots[0]
                                    });
                                    childMesh.Vertices.Add(new ObjModelReader.ObjVertex
                                    {
                                        Position = ops[1], ObjNormal = new ObjModelReader.ObjNormal(0, 0, 0),
                                        TexCood = ots[1]
                                    });
                                    childMesh.Vertices.Add(new ObjModelReader.ObjVertex
                                    {
                                        Position = ops[2], ObjNormal = new ObjModelReader.ObjNormal(0, 0, 0),
                                        TexCood = ots[2]
                                    });
                                    objMesh.ChildMeshes.Add(childMesh);
                                }
                            }
                        }
                    }
                }

                var objMeshes = new List<ObjModelReader.ObjMesh>();
                foreach (var c in meshes)
                {
                    objMeshes.Add(c.Value);
                }

                if (modelObj.TryGetPropertyValue("groups", out var groupsNode))
                {
                    //解析groups
                    var meshes2 = new Dictionary<string, ObjModelReader.ObjMesh>();
                    var groupsArray = (groupsNode as JsonArray)!;
                    for (var m = 0; m < groupsArray.Count; m++)
                    {
                        var groupObj = (groupsArray[m] as JsonObject)!;
                        var groupName = m.ToString();
                        if (groupObj.TryGetPropertyValue("name", out var nameNode))
                        {
                            groupName = nameNode!.ToString();
                        }

                        var mesh = new ObjModelReader.ObjMesh(groupName);
                        if (groupObj.TryGetPropertyValue("origin", out var originNode))
                        {
                            var originArray = (originNode as JsonArray)!;
                            var start = new Vector3(ObjConvertFloat(originArray[0]!), ObjConvertFloat(originArray[1]!),
                                ObjConvertFloat(originArray[2]!)) / 16f;
                            mesh.MeshMatrix = Matrix.CreateTranslation(start.X, start.Y, start.Z);
                        }

                        if (groupObj.TryGetPropertyValue("children", out var childrenNode))
                        {
                            var childrenArray = (childrenNode as JsonArray)!;
                            foreach (var item in childrenArray)
                            {
                                var childrenIndex = (int)ObjConvertFloat(item!);
                                var index = objMeshes.Find(xp => xp.ElementIndex == childrenIndex);
                                if(index is null)
                                {
                                    continue;
                                }

                                mesh.ChildMeshes.Add(index);
                            }
                        }

                        meshes2.Add(groupName, mesh);
                    }

                    var jsonModel = ObjModelReader.ObjMeshesToModel<JsonModel>(meshes2);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        try
                        {
                            jsonModel.ParentModel = ContentManager.Get<JsonModel>(parent);
                        }
                        catch
                        {
                            // ignored
                        }
                    }

                    jsonModel.InHandScale = inHandScale;
                    jsonModel.InHandOffset = inHandOffset;
                    jsonModel.InHandRotation = inHandRotation;
                    jsonModel.FirstPersonOffset = firstPersonOffset;
                    jsonModel.FirstPersonScale = firstPersonScale;
                    jsonModel.FirstPersonRotation = firstPersonRotation;
                    return jsonModel;
                }
            }
        }

        var jsonModel2 = ObjModelReader.ObjMeshesToModel<JsonModel>(meshes);
        if (!string.IsNullOrEmpty(parent))
        {
            try
            {
                jsonModel2.ParentModel = ContentManager.Get<JsonModel>(parent);
            }
            catch
            {
                // ignored
            }
        }

        jsonModel2.InHandScale = inHandScale;
        jsonModel2.InHandOffset = inHandOffset;
        jsonModel2.InHandRotation = inHandRotation;
        jsonModel2.FirstPersonOffset = firstPersonOffset;
        jsonModel2.FirstPersonScale = firstPersonScale;
        jsonModel2.FirstPersonRotation = firstPersonRotation;
        return jsonModel2;
    }
}
