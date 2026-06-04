using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemModelsRenderer : Subsystem, IDrawable
{
    public static ModelShader? ShaderOpaque;

    public static ModelShader? ShaderAlphaTested;

    public static bool DisableDrawingModels = false;

    private readonly Dictionary<ComponentModel, ModelData> _componentModels = new();

    private readonly int[] _drawOrders = [-10000, 1, 99, 201];

    private readonly List<ModelData>[] _modelsToDraw =
    [
        [],
        [],
        [],
        []
    ];

    private readonly List<ModelData> _modelsToPrepare = [];

    private ModelShader _shaderAlphaTested = null!;

    private ModelShader _shaderOpaque = null!;

    private SubsystemShadows _subsystemShadows = null!;

    private SubsystemSky _subsystemSky = null!;

    private SubsystemTerrain _subsystemTerrain = null!;

    private SubsystemTimeOfDay _subsystemTimeOfDay = null!;

    public Vector3 SunLightDirection { get; private set; }

    private int _maxInstancesCount;

    public int ModelsDrawn;

    private readonly List<DrawText> _signDatas = [];

    public PrimitivesRenderer3D PrimitivesRenderer { get; } = new();

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (drawOrder == _drawOrders[0])
        {
            ModelsDrawn = 0;
            foreach (var model in _modelsToDraw)
            {
                model.Clear();
            }

            _modelsToPrepare.Clear();
            foreach (var value in _componentModels.Values)
            {
                if (value.ComponentModel?.Model != null)
                {
                    value.ComponentModel.CalculateIsVisible(camera);
                    if (value.ComponentModel.IsVisibleForCamera)
                    {
                        _modelsToPrepare.Add(value);
                    }
                }
            }

            _modelsToPrepare.Sort();
            foreach (var item in _modelsToPrepare)
            {
                PrepareModel(item, camera);
                if (item.ComponentModel != null)
                {
                    _modelsToDraw[(int)item.ComponentModel.RenderingMode].Add(item);
                }
            }
        }

        if (!DisableDrawingModels)
        {
            SunLightDirection = 1.25f * Vector3.TransformNormal(SunVector(_subsystemSky), camera.ViewMatrix);
            if (drawOrder == _drawOrders[1])
            {
                Display.DepthStencilState = DepthStencilState.Default;
                Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
                Display.BlendState = BlendState.Opaque;
                DrawModels(camera, _modelsToDraw[0], null);
                Display.RasterizerState = RasterizerState.CullNoneScissor;
                DrawModels(camera, _modelsToDraw[1], 0f);
                Display.RasterizerState = RasterizerState.CullCounterClockwiseScissor;
                PrimitivesRenderer.Flush(camera.ProjectionMatrix, true, 0);
            }
            else if (drawOrder == _drawOrders[2])
            {
                Display.DepthStencilState = DepthStencilState.Default;
                Display.RasterizerState = RasterizerState.CullNoneScissor;
                Display.BlendState = BlendState.AlphaBlend;
                DrawModels(camera, _modelsToDraw[2], null);
            }
            else if (drawOrder == _drawOrders[3])
            {
                Display.DepthStencilState = DepthStencilState.Default;
                Display.RasterizerState = RasterizerState.CullNoneScissor;
                Display.BlendState = BlendState.AlphaBlend;
                DrawModels(camera, _modelsToDraw[3], null);
                if (ShaderOpaque != null && ShaderAlphaTested != null)
                {
                    PrimitivesRenderer.Flush(camera.ProjectionMatrix);
                }
                else
                {
                    PrimitivesRenderer.Flush(camera.ProjectionMatrix);
                }
            }
        }
        else
        {
            PrimitivesRenderer.Clear();
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemTimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _subsystemSky = Project.FindSubsystem<SubsystemSky>(true)!;
        _subsystemShadows = Project.FindSubsystem<SubsystemShadows>(true)!;
        ModsManager.HookAction("GetMaxInstancesCount", modLoader =>
        {
            _maxInstancesCount = Math.Max(modLoader.GetMaxInstancesCount(), _maxInstancesCount);
            return false;
        });
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        _shaderOpaque = new ModelShader(ShaderCodeManager.GetFast("Shaders/Model.vsh"),
            ShaderCodeManager.GetFast("Shaders/Model.psh"), false, _maxInstancesCount);
        _shaderAlphaTested = new ModelShader(ShaderCodeManager.GetFast("Shaders/Model.vsh"),
            ShaderCodeManager.GetFast("Shaders/Model.psh"), true, _maxInstancesCount);
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentModel>())
        {
            if (item == null)
            {
                continue;
            }

            var value = new ModelData
            {
                ComponentModel = item,
                ComponentBody = item.Entity.FindComponent<ComponentBody>(),
                Light = _subsystemSky.SkyLightIntensity
            };
            _componentModels.Add(item, value);
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<ComponentModel>())
        {
            if (item != null)
            {
                _componentModels.Remove(item);
            }
        }
    }

    public void PrepareModel(ModelData modelData, Camera camera)
    {
        if (Time.FrameIndex > modelData.LastAnimateFrame)
        {
            modelData.ComponentModel?.Animate();
            modelData.LastAnimateFrame = Time.FrameIndex;
        }

        if (Time.FrameStartTime >= modelData.NextLightTime)
        {
            var num = CalculateModelLight(modelData);
            if (num.HasValue)
            {
                modelData.Light = num.Value;
            }

            modelData.NextLightTime = Time.FrameStartTime + 0.1;
        }

        modelData.ComponentModel?.CalculateAbsoluteBonesTransforms(camera);
    }

    public void DrawModels(Camera camera, List<ModelData> modelsData, float? alphaThreshold)
    {
        DrawInstancedModels(camera, modelsData, alphaThreshold);
        DrawModelsExtras(camera, modelsData);
    }

    public void DrawInstancedModels(Camera camera, List<ModelData> modelsData, float? alphaThreshold)
    {
        ModelShader? modelShader;
        if (ShaderOpaque != null && ShaderAlphaTested != null)
        {
            modelShader = alphaThreshold.HasValue ? ShaderAlphaTested : ShaderOpaque;
        }
        else
        {
            modelShader = alphaThreshold.HasValue ? _shaderAlphaTested : _shaderOpaque;
        }

        modelShader.LightDirection1 = -Vector3.TransformNormal(LightingManager.DirectionToLight1, camera.ViewMatrix);
        modelShader.LightDirection2 = -Vector3.TransformNormal(LightingManager.DirectionToLight2, camera.ViewMatrix);
        modelShader.FogColor = new Vector3(_subsystemSky.ViewFogColor);
        modelShader.FogBottomTopDensity = new Vector3(_subsystemSky.ViewFogBottom - camera.ViewPosition.Y,
            _subsystemSky.ViewFogTop - camera.ViewPosition.Y, _subsystemSky.ViewFogDensity);
        modelShader.HazeStartDensity = new Vector2(_subsystemSky.ViewHazeStart, _subsystemSky.ViewHazeDensity);
        modelShader.FogYMultiplier = _subsystemSky.VisibilityRangeYMultiplier;
        modelShader.WorldUp = Vector3.TransformNormal(Vector3.UnitY, camera.ViewMatrix);
        modelShader.Transforms.View = Matrix.Identity;
        modelShader.Transforms.Projection = camera.ProjectionMatrix;
        modelShader.SamplerState = SamplerState.PointClamp;
        if (alphaThreshold.HasValue)
        {
            modelShader.AlphaThreshold = alphaThreshold.Value;
        }

        ModsManager.HookAction("ModelShaderParameter", modLoader =>
        {
            modLoader.ModelShaderParameter(modelShader, camera, modelsData, alphaThreshold);
            return true;
        });
        ModsManager.HookAction("SetShaderParameter", modLoader =>
        {
            modLoader.SetShaderParameter(modelShader, camera);
            return true;
        });
        foreach (var modelsDatum in modelsData)
        {
            var componentModel = modelsDatum.ComponentModel;
            if (componentModel == null)
            {
                continue;
            }

            var v = componentModel.DiffuseColor ?? Vector3.One;
            var num = componentModel.Opacity ?? 1f;
            modelShader.InstancesCount = componentModel.AbsoluteBoneTransformsForCamera.Length;
            modelShader.MaterialColor = new Vector4(v * num, num);
            modelShader.EmissionColor = componentModel.EmissionColor ?? Vector4.Zero;
            modelShader.AmbientLightColor = new Vector3(LightingManager.LightAmbient * modelsDatum.Light);
            modelShader.DiffuseLightColor1 = new Vector3(modelsDatum.Light);
            modelShader.DiffuseLightColor2 = new Vector3(modelsDatum.Light);
            if (componentModel.TextureOverride != null)
            {
                modelShader.Texture = componentModel.TextureOverride;
            }

            Array.Copy(componentModel.AbsoluteBoneTransformsForCamera, modelShader.Transforms.World,
                componentModel.AbsoluteBoneTransformsForCamera.Length);
            var instancedModelData =
                InstancedModelsManager.GetInstancedModelData(componentModel.Model, componentModel.MeshDrawOrders);
            Display.DrawIndexed(PrimitiveType.TriangleList, modelShader, instancedModelData.VertexBuffer,
                instancedModelData.IndexBuffer, 0, instancedModelData.IndexBuffer.IndicesCount);
            ModelsDrawn++;
            //画名称
            ModsManager.HookAction("OnModelRendererDrawExtra", modLoader =>
            {
                modLoader.OnModelRendererDrawExtra(this, componentModel, camera, alphaThreshold);
                return false;
            });
        }

        foreach (var obj in _signDatas)
        {
            if (Vector3.DistanceSquared(obj.Position, camera.ViewPosition) >= 100f)
            {
                continue;
            }

            var position = Vector3.Transform(obj.Position + 1.02f * Vector3.UnitY * 1, camera.ViewMatrix);
            if (!(position.Z < 0f))
            {
                continue;
            }

            var color = Color.Lerp(Color.White, Color.Transparent,
                MathUtils.Saturate((position.Length() - 4f) / 3f));
            if (color.A <= 8)
            {
                continue;
            }

            var right = Vector3.TransformNormal(
                0.005f * Vector3.Normalize(Vector3.Cross(camera.ViewDirection, Vector3.UnitY)),
                camera.ViewMatrix);
            var down = Vector3.TransformNormal(-0.005f * Vector3.UnitY, camera.ViewMatrix);
            var font = LabelWidget.BitmapFont;
            PrimitivesRenderer
                .FontBatch(font, 1, DepthStencilState.DepthRead, RasterizerState.CullNoneScissor,
                    BlendState.AlphaBlend, SamplerState.LinearClamp).QueueText(obj.Text, position, right, down,
                    color, TextAnchor.HorizontalCenter | TextAnchor.Bottom);
        }
    }

    public DrawText AddDrawText(Vector3 position, string text, Color color, double displaySeconds = 0,
        int maxHeight = 24)
    {
        var x = new DrawText { Position = position, Text = text, Color = color, MaxHeight = maxHeight };
        _signDatas.Add(x);
        if (displaySeconds > 0)
        {
            Time.QueueTimeDelayedExecution(Time.FrameStartTime + displaySeconds, () => { _signDatas.Remove(x); });
        }

        return x;
    }

    public void RemoveDrawText(DrawText drawText)
    {
        _signDatas.Remove(drawText);
    }

    public void DrawModelsExtras(Camera camera, List<ModelData> modelsData)
    {
        foreach (var modelData in modelsData)
        {
            if (modelData.ComponentBody != null && modelData.ComponentModel!.CastsShadow)
            {
                var shadowPosition = modelData.ComponentBody.Position + new Vector3(0f, 0.1f, 0f);
                var boundingBox = modelData.ComponentBody.BoundingBox;
                var shadowDiameter = 2.25f * (boundingBox.Max.X - boundingBox.Min.X);
                _subsystemShadows.QueueShadow(camera, shadowPosition, shadowDiameter,
                    modelData.ComponentModel.Opacity ?? 1f);
            }

            modelData.ComponentModel?.DrawExtras(camera);
        }
    }

    public float? CalculateModelLight(ModelData modelData)
    {
        var p = Vector3.Zero;
        if (modelData.ComponentBody != null)
        {
            p = modelData.ComponentBody.Position;
            p.Y += 0.95f * (modelData.ComponentBody.BoundingBox.Max.Y - modelData.ComponentBody.BoundingBox.Min.Y);
        }
        else
        {
            if (modelData.ComponentModel == null)
            {
                return LightingManager.CalculateSmoothLight(_subsystemTerrain, p);
            }

            var boneTransform =
                modelData.ComponentModel.GetBoneTransform(modelData.ComponentModel.Model.RootBone.Index);
            p = !boneTransform.HasValue ? Vector3.Zero : boneTransform.Value.Translation + new Vector3(0f, 0.9f, 0f);
        }

        return LightingManager.CalculateSmoothLight(_subsystemTerrain, p);
    }

    //太阳向量
    private Vector3 SunVector(SubsystemSky subsystemSky)
    {
        var timeOfDay = subsystemSky.SubsystemTimeOfDay.TimeOfDay;
        var num = 2f * timeOfDay * (float)Math.PI;
        var x = num + (float)Math.PI;
        var f = MathUtils.Max(subsystemSky.CalculateDawnGlowIntensity(timeOfDay),
            subsystemSky.CalculateDuskGlowIntensity(timeOfDay));
        var s = MathUtils.Lerp(90f, 160f, f);
        var vector = new Vector3
        {
            X = 0f - MathUtils.Sin(x),
            Y = 0f - MathUtils.Cos(x),
            Z = 0f
        };
        var unitZ = Vector3.UnitZ;
        var v = Vector3.Cross(unitZ, vector);
        var v2 = vector * 900f - s * unitZ - num * v;
        return Vector3.Normalize(v2);
    }

    //阴影绘制
    public void ShadowDraw(SubsystemShadows subsystemShadows, Camera camera, Vector3 shadowPosition,
        float shadowDiameter, float alpha)
    {
        if (!SettingsManager.ObjectsShadowsEnabled)
        {
            return;
        }

        var num = Vector3.DistanceSquared(camera.ViewPosition, shadowPosition);
        if (!(num <= 1024f))
        {
            return;
        }

        var num2 = MathUtils.Sqrt(num);
        var num3 = MathUtils.Saturate(4f * (1f - num2 / 32f));
        var num4 = shadowDiameter / 2f; //阴影直径/2
        var num5 = Terrain.ToCell(shadowPosition.X - num4);
        var num6 = Terrain.ToCell(shadowPosition.Z - num4);
        var num7 = Terrain.ToCell(shadowPosition.X + num4);
        var num8 = Terrain.ToCell(shadowPosition.Z + num4);
        for (var i = num5; i <= num7; i++)
        for (var j = num6; j <= num8; j++)
        {
            var num9 = MathUtils.Min(Terrain.ToCell(shadowPosition.Y), 255);
            var num10 = MathUtils.Max(num9 - 2, 0);
            for (var num11 = num9; num11 >= num10; num11--)
            {
                var cellValueFast = subsystemShadows.SubsystemTerrain.Terrain.GetCellValueFast(i, num11, j);
                var num12 = Terrain.ExtractContents(cellValueFast);
                var block = BlocksManager.Blocks[num12];
                if (block.ObjectShadowStrength > 0f)
                {
                    var customCollisionBoxes =
                        block.GetCustomCollisionBoxes(subsystemShadows.SubsystemTerrain, cellValueFast);
                    foreach (var boundingBox in customCollisionBoxes)
                    {
                        var num13 = boundingBox.Max.Y + num11;
                        if (!(shadowPosition.Y - num13 > -0.5f))
                        {
                            continue;
                        }

                        var num14 = camera.ViewPosition.Y - num13;
                        if (!(num14 > 0f))
                        {
                            continue;
                        }

                        var num15 = MathUtils.Max(num14 * 0.01f, 0.005f);
                        var num16 = MathUtils.Saturate(1f - (shadowPosition.Y - num13) / 2f);
                        var p = new Vector3(boundingBox.Min.X + i, num13 + num15, boundingBox.Min.Z + j);
                        var p2 = new Vector3(boundingBox.Max.X + i, num13 + num15, boundingBox.Min.Z + j);
                        var p3 = new Vector3(boundingBox.Max.X + i, num13 + num15, boundingBox.Max.Z + j);
                        var p4 = new Vector3(boundingBox.Min.X + i, num13 + num15, boundingBox.Max.Z + j);
                        subsystemShadows.DrawShadowOverQuad(p, p2, p3, p4, shadowPosition, shadowDiameter,
                            0.45f * block.ObjectShadowStrength * alpha * num3 * num16);
                    }

                    break;
                }

                if (num12 == 18)
                {
                    break;
                }
            }
        }
    }

    public class ModelData : IComparable<ModelData>
    {
        public ComponentBody? ComponentBody;

        public ComponentModel? ComponentModel;

        public int LastAnimateFrame;

        public float Light;

        public double NextLightTime;

        public int CompareTo(ModelData? other)
        {
            var num = ComponentModel?.PrepareOrder ?? 0;
            var num2 = other?.ComponentModel?.PrepareOrder ?? 0;
            return num - num2;
        }
    }
}
