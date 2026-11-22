using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemShadows : Subsystem, IDrawable
{
    private static readonly int[] _drawOrders = [200];

    private TexturedBatch3D _batch = null!;

    private SubsystemSkyPresentation _subsystemSky = null!;

    private SubsystemBlockPresentation _blockPresentation = null!;

    private PrimitivesRenderer3D PrimitivesRenderer3D => field ??= new();

    public SubsystemTerrain SubsystemTerrain = null!;

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        PrimitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
    }

    public void QueueShadow(Camera camera, Vector3 shadowPosition, float shadowDiameter, float alpha)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        if (!Configuration.ClientConfiguration.Preferences.ObjectsShadowsEnabled)
        {
            return;
        }

        var distanceSquared = Vector3.DistanceSquared(camera.ViewPosition, shadowPosition);
        if (!(distanceSquared <= 1024f))
        {
            return;
        }

        var cameraDistance = MathUtils.Sqrt(distanceSquared);
        var distanceFade = MathUtils.Saturate(4f * (1f - cameraDistance / 32f));
        distanceFade *= 1f - _subsystemSky.CalculateFog(camera.ViewPosition, shadowPosition);
        if (distanceFade == 0f) { return; }

        var blocks = Modding.Blocks.WorldContentBinding.For(Project).Catalog.Blocks;
        var presentation = _blockPresentation.Catalog;
        var terrain = SubsystemTerrain.Terrain;
        var radius = shadowDiameter / 2f;
        var minX = Terrain.ToCell(shadowPosition.X - radius);
        var minZ = Terrain.ToCell(shadowPosition.Z - radius);
        var maxX = Terrain.ToCell(shadowPosition.X + radius);
        var maxZ = Terrain.ToCell(shadowPosition.Z + radius);
        var topY = MathUtils.Min(Terrain.ToCell(shadowPosition.Y), 255);
        var bottomY = MathUtils.Max(topY - 2, 0);
        for (var i = minX; i <= maxX; i++)
        {
            for (var j = minZ; j <= maxZ; j++)
            {
                for (var cellY = topY; cellY >= bottomY; cellY--)
                {
                    var cellValueFast = terrain.GetCellValueFast(i, cellY, j);
                    var contents = Terrain.ExtractContents(cellValueFast);
                    var shadowStrength = presentation.Get(contents).ObjectShadowStrength;
                    if (shadowStrength > 0f)
                    {
                        var customCollisionBoxes =
                            blocks[contents].GetCustomCollisionBoxes(SubsystemTerrain, cellValueFast);
                        foreach (var boundingBox in customCollisionBoxes)
                        {
                            var surfaceY = boundingBox.Max.Y + cellY;
                            if (!(shadowPosition.Y - surfaceY > -0.5f))
                            {
                                continue;
                            }

                            var cameraHeight = camera.ViewPosition.Y - surfaceY;
                            if (!(cameraHeight > 0f))
                            {
                                continue;
                            }

                            var surfaceOffset = MathUtils.Max(cameraHeight * 0.01f, 0.005f);
                            var heightFade = MathUtils.Saturate(1f - (shadowPosition.Y - surfaceY) / 2f);
                            var p = new Vector3(boundingBox.Min.X + i, surfaceY + surfaceOffset, boundingBox.Min.Z + j);
                            var p2 = new Vector3(
                                boundingBox.Max.X + i,
                                surfaceY + surfaceOffset,
                                boundingBox.Min.Z + j
                            );
                            var p3 = new Vector3(
                                boundingBox.Max.X + i,
                                surfaceY + surfaceOffset,
                                boundingBox.Max.Z + j
                            );
                            var p4 = new Vector3(
                                boundingBox.Min.X + i,
                                surfaceY + surfaceOffset,
                                boundingBox.Max.Z + j
                            );
                            DrawShadowOverQuad(
                                p,
                                p2,
                                p3,
                                p4,
                                shadowPosition,
                                shadowDiameter,
                                0.45f * shadowStrength * alpha * distanceFade * heightFade
                            );
                        }

                        break;
                    }

                    if (contents == 18)
                    {
                        break;
                    }
                }
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        _subsystemSky = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
        _blockPresentation = Project.FindSubsystem<SubsystemBlockPresentation>(true)!;
        _batch = PrimitivesRenderer3D.TexturedBatch(
            ContentManager.Get<Texture2D>("Textures/Shadow"),
            false,
            0,
            DepthStencilState.DepthRead,
            RasterizerState.CullCounterClockwiseScissor,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp
        );
    }

    public void DrawShadowOverQuad(
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        Vector3 p4,
        Vector3 shadowPosition,
        float shadowDiameter,
        float alpha
    )
    {
        if (!(alpha > 0.02f))
        {
            return;
        }

        var texCoord = CalculateShadowTextureCoordinate(p1, shadowPosition, shadowDiameter);
        var texCoord2 = CalculateShadowTextureCoordinate(p2, shadowPosition, shadowDiameter);
        var texCoord3 = CalculateShadowTextureCoordinate(p3, shadowPosition, shadowDiameter);
        var texCoord4 = CalculateShadowTextureCoordinate(p4, shadowPosition, shadowDiameter);
        _batch.QueueQuad(p1, p2, p3, p4, texCoord, texCoord2, texCoord3, texCoord4, new Color(0f, 0f, 0f, alpha));
    }

    private static Vector2 CalculateShadowTextureCoordinate(Vector3 p, Vector3 shadowPosition, float shadowDiameter)
    {
        return new Vector2(
            0.5f + (p.X - shadowPosition.X) / shadowDiameter,
            0.5f + (p.Z - shadowPosition.Z) / shadowDiameter
        );
    }
}
