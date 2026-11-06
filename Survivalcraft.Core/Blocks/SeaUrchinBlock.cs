using Game.Blocks.Collision;

namespace Game.Blocks;

public class SeaUrchinBlock : BottomSuckerBlock
{
    public static Color[] Colors =
    [
        new(20, 20, 20),
        new(50, 20, 20),
        new(80, 30, 30),
        new(20, 20, 40)
    ];

    public static Vector2[] Offsets =
    [
        0.15f * new Vector2(-0.8f, -1f),
        0.15f * new Vector2(1f, -0.75f),
        0.15f * new Vector2(-0.65f, 1f),
        0.15f * new Vector2(0.9f, 0.7f)
    ];


    public BoundingBox[][] CollisionBoxes = new BoundingBox[24][];


    public override ResourceId? CollisionModelAsset { get; } = ResourceId.Parse("game:Models/SeaUrchin.glb");

    public override void Initialize()
    {
        var blockPositions = new CollisionGeometryBuilder[24];
        var model = Catalog.CollisionGeometry.Get(CollisionModelAsset!.Value);
        var urchinNode = model.GetNode("Urchin")!;
        var nodeAbsoluteTransform = urchinNode.AbsoluteTransform;
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
                var num = 4 * i + j;
                blockPositions[num] = new CollisionGeometryBuilder();
                blockPositions[num].Append(urchinNode.Primitives[0], nodeAbsoluteTransform * m);
                CollisionBoxes[num] = [blockPositions[num].GetBounds()];
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
