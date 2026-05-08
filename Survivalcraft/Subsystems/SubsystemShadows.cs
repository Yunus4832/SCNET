using Engine.Graphics;
using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemShadows : Subsystem, IDrawable
{
    private static readonly int[] _drawOrders = [200];

    private TexturedBatch3D _batch = null!;

    private readonly PrimitivesRenderer3D _primitivesRenderer = new();

    public SubsystemTerrain SubsystemTerrain = null!;

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        _primitivesRenderer.Flush(camera.ViewProjectionMatrix);
    }

    public void QueueShadow(Camera camera, Vector3 shadowPosition, float shadowDiameter, float alpha)
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
        var num4 = shadowDiameter / 2f;
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
                var cellValueFast = SubsystemTerrain.Terrain.GetCellValueFast(i, num11, j);
                var num12 = Terrain.ExtractContents(cellValueFast);
                var block = BlocksManager.Blocks[num12];
                if (block.ObjectShadowStrength > 0f)
                {
                    var customCollisionBoxes = block.GetCustomCollisionBoxes(SubsystemTerrain, cellValueFast);
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
                        DrawShadowOverQuad(p, p2, p3, p4, shadowPosition, shadowDiameter,
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

    public override void Load(ValuesDictionary valuesDictionary)
    {
        SubsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _batch = _primitivesRenderer.TexturedBatch(ContentManager.Get<Texture2D>("Textures/Shadow"), false, 0,
            DepthStencilState.DepthRead, RasterizerState.CullCounterClockwiseScissor, BlendState.AlphaBlend,
            SamplerState.LinearClamp);
    }

    public void DrawShadowOverQuad(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 shadowPosition,
        float shadowDiameter, float alpha)
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
        return new Vector2(0.5f + (p.X - shadowPosition.X) / shadowDiameter,
            0.5f + (p.Z - shadowPosition.Z) / shadowDiameter);
    }
}
