using Engine.Graphics;

namespace Game.Widgets;

public class BlockIconWidget : Widget
{
    private Matrix _viewMatrix;

    public DrawBlockEnvironmentData DrawBlockEnvironmentData { get; set; }

    public Vector2 Size { get; set; }

    public float Depth { get; set; }

    public Color Color { get; set; }

    public Matrix? CustomViewMatrix { get; set; }

    public int Value
    {
        get;
        set
        {
            if (field != 0 && value == field)
            {
                return;
            }

            field = value;
            var block = BlocksManager.Blocks[Contents];
            _viewMatrix = Matrix.CreateLookAt(block.GetIconViewOffset(Value, DrawBlockEnvironmentData),
                new Vector3(0f, 0f, 0f), Vector3.UnitY);
        }
    }

    public int Contents
    {
        get => Terrain.ExtractContents(Value);
        set => Value = Terrain.ReplaceContents(Value, value);
    }

    public int Light
    {
        get => Terrain.ExtractLight(Value);
        set => Value = Terrain.ReplaceLight(Value, value);
    }

    public int Data
    {
        get => Terrain.ExtractData(Value);
        set => Value = Terrain.ReplaceData(Value, value);
    }

    public float Scale { get; set; }

    public override bool IsHitTestVisible { get; set; } = false;

    public BlockIconWidget()
    {
        DrawBlockEnvironmentData = new DrawBlockEnvironmentData();
        Size = new Vector2(float.PositiveInfinity);
        Light = 15;
        Depth = 1f;
        Color = Color.White;
        Scale = 1f;
    }


    public override void Draw(DrawContext dc)
    {
        var obj = BlocksManager.Blocks[Contents];
        _ = DrawBlockEnvironmentData.SubsystemTerrain != null
            ? DrawBlockEnvironmentData.SubsystemTerrain.SubsystemAnimatedTextures.AnimatedBlocksTexture
            : BlocksTexturesManager.DefaultBlocksTexture;
        var viewport = Display.Viewport;
        var num = MathUtils.Min(ActualSize.X, ActualSize.Y) * Scale;
        var m = Matrix.CreateOrthographic(3.6f, 3.6f, -10f - 1f * Depth, 10f - 1f * Depth);
        var m2 = MatrixUtils.CreateScaleTranslation(num, 0f - num, ActualSize.X / 2f, ActualSize.Y / 2f) *
                 GlobalTransform *
                 MatrixUtils.CreateScaleTranslation(2f / viewport.Width, -2f / viewport.Height, -1f, 1f);
        DrawBlockEnvironmentData.DrawBlockMode = DrawBlockMode.Ui;
        DrawBlockEnvironmentData.ViewProjectionMatrix =
            (CustomViewMatrix ?? _viewMatrix) * m * m2;
        var iconViewScale = BlocksManager.Blocks[Contents].GetIconViewScale(Value, DrawBlockEnvironmentData);
        var matrix = CustomViewMatrix.HasValue
            ? Matrix.Identity
            : Matrix.CreateTranslation(BlocksManager.Blocks[Contents]
                .GetIconBlockOffset(Value, DrawBlockEnvironmentData));
        obj.DrawBlock(dc.PrimitivesRenderer3D, Value, GlobalColorTransform, iconViewScale, ref matrix,
            DrawBlockEnvironmentData);
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        DesiredSize = Size;
    }
}
