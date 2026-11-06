using Game.Blocks.Collision;

namespace Game.Blocks;

public class StarfishBlock : BottomSuckerBlock
{
    public static Color[] Colors =
    [
        new(100, 40, 20),
        new(90, 30, 20),
        new(100, 30, 30),
        new(80, 20, 10)
    ];

    public static Vector2[] Offsets =
    [
        0.15f * new Vector2(-0.8f, -1f),
        0.15f * new Vector2(1f, -0.75f),
        0.15f * new Vector2(-0.65f, 1f),
        0.15f * new Vector2(0.9f, 0.7f)
    ];


    public BoundingBox[][] CollisionBoxes = new BoundingBox[24][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/Starfish.glb");

    public override void Initialize()
    {
        var blockPositions = new CollisionGeometryBuilder[24];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var starfishNode = model.GetNode("Starfish")!;
        var nodeAbsoluteTransform = starfishNode.AbsoluteTransform;
        for (var i = 0; i < 6; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                var zero = Vector2.Zero;
                if (i < 4)
                {
                    zero.Y = i * (float)Math.PI / 2f;
                }
                else
                {
                    zero.X = i == 4 ? -(float)Math.PI / 2f : (float)Math.PI / 2f;
                }

                var m = Matrix.CreateRotationX((float)Math.PI / 2f) *
                        Matrix.CreateRotationZ(0.3f + 2f * j) *
                        Matrix.CreateTranslation(Offsets[j].X, Offsets[j].Y, -0.49f) *
                        Matrix.CreateRotationX(zero.X) *
                        Matrix.CreateRotationY(zero.Y) *
                        Matrix.CreateTranslation(0.5f, 0.5f, 0.5f);
                var num2 = 4 * i + j;
                blockPositions[num2] = new CollisionGeometryBuilder();
                blockPositions[num2].Append(starfishNode.Primitives[0], nodeAbsoluteTransform * m);
                CollisionBoxes[num2] = [blockPositions[num2].GetBounds()];
            }
        }

        base.Initialize();
    }


    public override BoundingBox[] GetCustomCollisionBoxes(SubsystemTerrain terrain, int value)
    {
        var data = Terrain.ExtractData(value);
        var face = GetFace(data);
        var subvariant = GetSubvariant(data);
        return CollisionBoxes[4 * face + subvariant];
    }
}
