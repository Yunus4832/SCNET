using Game.Network;
using Game.Network.Enums;

namespace Game.Widgets;

public class GamesWidget : ContainerWidget
{
    private float _bevel;

    private float _spacing;

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        base.MeasureOverride(parentAvailableSize);
        IsOverdrawRequired = Children.Count > 1;
    }

    public override void ArrangeOverride()
    {
        if (CommonLib.WorkType != WorkType.Local && Children.Count > 0)
        {
            foreach (var child in Children)
            {
                var gameWidget = (GameWidget)child;
                gameWidget.IsVisible = gameWidget.PlayerData.IsMainPlayer;
                if (!gameWidget.IsVisible)
                {
                    continue;
                }

                ArrangeChildWidgetInCell(Vector2.Zero, ActualSize, child);
                gameWidget.LayoutTransform = Matrix.Identity;
            }
        }
        else
        {
            if (Children.Count == 1)
            {
                ArrangeChildWidgetInCell(Vector2.Zero, ActualSize, Children[0]);
                if (SettingsManager.ScreenLayout1 == ScreenLayout.Single)
                {
                    Children[0].LayoutTransform = Matrix.Identity;
                }
            }
            else if (Children.Count == 2)
            {
                if (SettingsManager.ScreenLayout2 == ScreenLayout.DoubleVertical)
                {
                    _spacing = 12f;
                    _bevel = 3f;
                    var x = 0f;
                    var y = 0f;
                    var x2 = ActualSize.X / 2f + _spacing / 2f;
                    var y2 = 0f;
                    var x3 = ActualSize.X / 2f - _spacing / 2f;
                    var y3 = ActualSize.Y;
                    var num = 0.5f;
                    ArrangeChildWidgetInCell(new Vector2(x, y), new Vector2(x, y) + new Vector2(x3, y3) / num,
                        Children[0]);
                    Children[0].LayoutTransform = Matrix.CreateScale(num, num, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x2, y2), new Vector2(x2, y2) + new Vector2(x3, y3) / num,
                        Children[1]);
                    Children[1].LayoutTransform = Matrix.CreateScale(num, num, 1f);
                }

                if (SettingsManager.ScreenLayout2 == ScreenLayout.DoubleHorizontal)
                {
                    _spacing = 12f;
                    _bevel = 3f;
                    var x4 = 0f;
                    var y4 = 0f;
                    var x5 = 0f;
                    var y5 = ActualSize.Y / 2f + _spacing / 2f;
                    var x6 = ActualSize.X;
                    var y6 = ActualSize.Y / 2f - _spacing / 2f;
                    var num2 = 0.48f;
                    ArrangeChildWidgetInCell(new Vector2(x4, y4), new Vector2(x4, y4) + new Vector2(x6, y6) / num2,
                        Children[0]);
                    Children[0].LayoutTransform = Matrix.CreateScale(num2, num2, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x5, y5), new Vector2(x5, y5) + new Vector2(x6, y6) / num2,
                        Children[1]);
                    Children[1].LayoutTransform = Matrix.CreateScale(num2, num2, 1f);
                }

                if (SettingsManager.ScreenLayout2 == ScreenLayout.DoubleOpposite)
                {
                    _spacing = 20f;
                    _bevel = 4f;
                    var x7 = 0f;
                    var y7 = 0f;
                    var x8 = ActualSize.X / 2f + _spacing / 2f;
                    var y8 = 0f;
                    var x9 = ActualSize.X / 2f - _spacing / 2f;
                    var y9 = ActualSize.Y;
                    var num3 = Window.Size.Y / (float)Window.Size.X;
                    ArrangeChildWidgetInCell(new Vector2(x7, y7), new Vector2(x7, y7) + new Vector2(x9, y9) / num3,
                        Children[0]);
                    Children[0].LayoutTransform = new Matrix(0f, num3, 0f, 0f, 0f - num3, 0f, 0f, 0f, 0f, 0f, 1f, 0f,
                        0f, 0f, 0f, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x8, y8), new Vector2(x8, y8) + new Vector2(x9, y9) / num3,
                        Children[1]);
                    Children[1].LayoutTransform = new Matrix(0f, 0f - num3, 0f, 0f, num3, 0f, 0f, 0f, 0f, 0f, 1f, 0f,
                        0f, 0f, 0f, 1f);
                }
            }
            else if (Children.Count == 3)
            {
                _spacing = 12f;
                _bevel = 3f;
                if (SettingsManager.ScreenLayout3 == ScreenLayout.TripleVertical)
                {
                    var x10 = 0f;
                    var y10 = 0f;
                    var x11 = ActualSize.X / 2f + _spacing / 2f;
                    var y11 = 0f;
                    var x12 = ActualSize.X / 2f + _spacing / 2f;
                    var y12 = ActualSize.Y / 2f + _spacing / 2f;
                    var x13 = ActualSize.X / 2f - _spacing / 2f;
                    var y13 = ActualSize.Y;
                    var y14 = ActualSize.Y / 2f - _spacing / 2f;
                    var num4 = 0.5f;
                    ArrangeChildWidgetInCell(new Vector2(x10, y10),
                        new Vector2(x10, y10) + new Vector2(x13, y13) / num4, Children[0]);
                    Children[0].LayoutTransform = Matrix.CreateScale(num4, num4, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x11, y11),
                        new Vector2(x11, y11) + new Vector2(x13, y14) / num4, Children[1]);
                    Children[1].LayoutTransform = Matrix.CreateScale(num4, num4, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x12, y12),
                        new Vector2(x12, y12) + new Vector2(x13, y14) / num4, Children[2]);
                    Children[2].LayoutTransform = Matrix.CreateScale(num4, num4, 1f);
                }

                if (SettingsManager.ScreenLayout3 == ScreenLayout.TripleHorizontal)
                {
                    var x14 = 0f;
                    var y15 = 0f;
                    var x15 = 0f;
                    var y16 = ActualSize.Y / 2f + _spacing / 2f;
                    var x16 = ActualSize.X / 2f + _spacing / 2f;
                    var y17 = ActualSize.Y / 2f + _spacing / 2f;
                    var x17 = ActualSize.X;
                    var x18 = ActualSize.X / 2f - _spacing / 2f;
                    var y18 = ActualSize.Y / 2f - _spacing / 2f;
                    var num5 = 0.5f;
                    ArrangeChildWidgetInCell(new Vector2(x14, y15),
                        new Vector2(x14, y15) + new Vector2(x17, y18) / num5, Children[0]);
                    Children[0].LayoutTransform = Matrix.CreateScale(num5, num5, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x15, y16),
                        new Vector2(x15, y16) + new Vector2(x18, y18) / num5, Children[1]);
                    Children[1].LayoutTransform = Matrix.CreateScale(num5, num5, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x16, y17),
                        new Vector2(x16, y17) + new Vector2(x18, y18) / num5, Children[2]);
                    Children[2].LayoutTransform = Matrix.CreateScale(num5, num5, 1f);
                }

                if (SettingsManager.ScreenLayout3 == ScreenLayout.TripleEven)
                {
                    var x19 = 0f;
                    var y19 = 0f;
                    var x20 = ActualSize.X / 2f + _spacing / 2f;
                    var y20 = 0f;
                    var x21 = ActualSize.X / 4f + _spacing / 4f;
                    var y21 = ActualSize.Y / 2f + _spacing / 2f;
                    var x22 = ActualSize.X / 2f - _spacing / 2f;
                    var y22 = ActualSize.Y / 2f - _spacing / 2f;
                    var num6 = 0.5f;
                    ArrangeChildWidgetInCell(new Vector2(x19, y19),
                        new Vector2(x19, y19) + new Vector2(x22, y22) / num6, Children[0]);
                    Children[0].LayoutTransform = Matrix.CreateScale(num6, num6, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x20, y20),
                        new Vector2(x20, y20) + new Vector2(x22, y22) / num6, Children[1]);
                    Children[1].LayoutTransform = Matrix.CreateScale(num6, num6, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x21, y21),
                        new Vector2(x21, y21) + new Vector2(x22, y22) / num6, Children[2]);
                    Children[2].LayoutTransform = Matrix.CreateScale(num6, num6, 1f);
                }

                if (SettingsManager.ScreenLayout3 == ScreenLayout.TripleOpposite)
                {
                    var x23 = 0f;
                    var y23 = 0f;
                    var x24 = ActualSize.X / 2f + _spacing / 2f;
                    var y24 = 0f;
                    var x25 = ActualSize.X / 2f + _spacing / 2f;
                    var y25 = ActualSize.Y / 2f + _spacing / 2f;
                    var x26 = ActualSize.X / 2f - _spacing / 2f;
                    var y26 = ActualSize.Y;
                    var y27 = ActualSize.Y / 2f - _spacing / 2f;
                    var num7 = 0.5f;
                    ArrangeChildWidgetInCell(new Vector2(x23, y23),
                        new Vector2(x23, y23) + new Vector2(x26, y26) / num7, Children[0]);
                    Children[0].LayoutTransform = new Matrix(0f, num7, 0f, 0f, 0f - num7, 0f, 0f, 0f, 0f, 0f, 1f, 0f,
                        0f, 0f, 0f, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x24, y24),
                        new Vector2(x24, y24) + new Vector2(x26, y27) / num7, Children[1]);
                    Children[1].LayoutTransform = new Matrix(0f - num7, 0f, 0f, 0f, 0f, 0f - num7, 0f, 0f, 0f, 0f, 1f,
                        0f, 0f, 0f, 0f, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x25, y25),
                        new Vector2(x25, y25) + new Vector2(x26, y27) / num7, Children[2]);
                    Children[2].LayoutTransform =
                        new Matrix(num7, 0f, 0f, 0f, 0f, num7, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);
                }
            }
            else if (Children.Count == 4)
            {
                if (SettingsManager.ScreenLayout4 == ScreenLayout.Quadruple)
                {
                    _spacing = 12f;
                    _bevel = 3f;
                    var x27 = 0f;
                    var y28 = 0f;
                    var x28 = ActualSize.X / 2f + _spacing / 2f;
                    var y29 = 0f;
                    var x29 = 0f;
                    var y30 = ActualSize.Y / 2f + _spacing / 2f;
                    var x30 = ActualSize.X / 2f + _spacing / 2f;
                    var y31 = ActualSize.Y / 2f + _spacing / 2f;
                    var x31 = ActualSize.X / 2f - _spacing / 2f;
                    var y32 = ActualSize.Y / 2f - _spacing / 2f;
                    var num8 = 0.5f;
                    ArrangeChildWidgetInCell(new Vector2(x27, y28),
                        new Vector2(x27, y28) + new Vector2(x31, y32) / num8, Children[0]);
                    Children[0].LayoutTransform = Matrix.CreateScale(num8, num8, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x28, y29),
                        new Vector2(x28, y29) + new Vector2(x31, y32) / num8, Children[1]);
                    Children[1].LayoutTransform = Matrix.CreateScale(num8, num8, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x29, y30),
                        new Vector2(x29, y30) + new Vector2(x31, y32) / num8, Children[2]);
                    Children[2].LayoutTransform = Matrix.CreateScale(num8, num8, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x30, y31),
                        new Vector2(x30, y31) + new Vector2(x31, y32) / num8, Children[3]);
                    Children[3].LayoutTransform = Matrix.CreateScale(num8, num8, 1f);
                }

                if (SettingsManager.ScreenLayout4 == ScreenLayout.QuadrupleOpposite)
                {
                    _spacing = 12f;
                    _bevel = 3f;
                    var x32 = 0f;
                    var y33 = 0f;
                    var x33 = ActualSize.X / 2f + _spacing / 2f;
                    var y34 = 0f;
                    var x34 = 0f;
                    var y35 = ActualSize.Y / 2f + _spacing / 2f;
                    var x35 = ActualSize.X / 2f + _spacing / 2f;
                    var y36 = ActualSize.Y / 2f + _spacing / 2f;
                    var x36 = ActualSize.X / 2f - _spacing / 2f;
                    var y37 = ActualSize.Y / 2f - _spacing / 2f;
                    var num9 = 0.5f;
                    ArrangeChildWidgetInCell(new Vector2(x32, y33),
                        new Vector2(x32, y33) + new Vector2(x36, y37) / num9, Children[0]);
                    Children[0].LayoutTransform = new Matrix(0f - num9, 0f, 0f, 0f, 0f, 0f - num9, 0f, 0f, 0f, 0f, 1f,
                        0f, 0f, 0f, 0f, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x33, y34),
                        new Vector2(x33, y34) + new Vector2(x36, y37) / num9, Children[1]);
                    Children[1].LayoutTransform = new Matrix(0f - num9, 0f, 0f, 0f, 0f, 0f - num9, 0f, 0f, 0f, 0f, 1f,
                        0f, 0f, 0f, 0f, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x34, y35),
                        new Vector2(x34, y35) + new Vector2(x36, y37) / num9, Children[2]);
                    Children[2].LayoutTransform =
                        new Matrix(num9, 0f, 0f, 0f, 0f, num9, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);
                    ArrangeChildWidgetInCell(new Vector2(x35, y36),
                        new Vector2(x35, y36) + new Vector2(x36, y37) / num9, Children[3]);
                    Children[3].LayoutTransform =
                        new Matrix(num9, 0f, 0f, 0f, 0f, num9, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f);
                }
            }
        }
    }

    public override void Overdraw(DrawContext dc)
    {
        var color = new Color(181, 172, 154) * GlobalColorTransform;
        var num = 0.6f;
        var directionalLight = 0.4f;
        var flatBatch2D = dc.PrimitivesRenderer2D.FlatBatch();
        var count = flatBatch2D.TriangleVertices.Count;
        if (CommonLib.WorkType == WorkType.Local)
        {
            if (Children.Count == 2)
            {
                if (SettingsManager.ScreenLayout2 == ScreenLayout.DoubleVertical ||
                    SettingsManager.ScreenLayout2 == ScreenLayout.DoubleOpposite)
                {
                    var c = new Vector2(ActualSize.X / 2f - _spacing / 2f, -100f);
                    var c2 = new Vector2(ActualSize.X / 2f + _spacing / 2f, ActualSize.Y + 100f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, c, c2, 0f, _bevel, color, color,
                        Color.Transparent, num, directionalLight, 0f);
                }

                if (SettingsManager.ScreenLayout2 == ScreenLayout.DoubleHorizontal)
                {
                    var c3 = new Vector2(-100f, ActualSize.Y / 2f - _spacing / 2f);
                    var c4 = new Vector2(ActualSize.X + 100f, ActualSize.Y / 2f + _spacing / 2f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, c3, c4, 0f, _bevel, color, color,
                        Color.Transparent, num, directionalLight, 0f);
                }
            }
            else if (Children.Count == 3)
            {
                if (SettingsManager.ScreenLayout3 == ScreenLayout.TripleVertical ||
                    SettingsManager.ScreenLayout3 == ScreenLayout.TripleOpposite)
                {
                    var x = -100f;
                    var x2 = ActualSize.X / 2f - _spacing / 2f + _bevel;
                    var x3 = ActualSize.X / 2f + _spacing / 2f - _bevel;
                    var x4 = ActualSize.X + 100f;
                    var y = -100f;
                    var y2 = ActualSize.Y / 2f - _spacing / 2f + _bevel;
                    var y3 = ActualSize.Y / 2f + _spacing / 2f - _bevel;
                    var y4 = ActualSize.Y + 100f;
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x, y),
                        new Vector2(x2, y4), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x3, y),
                        new Vector2(x4, y2), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x3, y3),
                        new Vector2(x4, y4), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    var color2 = color * new Color(num, num, num, 1f);
                    flatBatch2D.QueueQuad(new Vector2(x2, y), new Vector2(x3, y4), 0f, color2);
                    flatBatch2D.QueueQuad(new Vector2(x3, y2), new Vector2(x4, y3), 0f, color2);
                }

                if (SettingsManager.ScreenLayout3 == ScreenLayout.TripleHorizontal)
                {
                    var x5 = -100f;
                    var x6 = ActualSize.X / 2f - _spacing / 2f + _bevel;
                    var x7 = ActualSize.X / 2f + _spacing / 2f - _bevel;
                    var x8 = ActualSize.X + 100f;
                    var y5 = -100f;
                    var y6 = ActualSize.Y / 2f - _spacing / 2f + _bevel;
                    var y7 = ActualSize.Y / 2f + _spacing / 2f - _bevel;
                    var y8 = ActualSize.Y + 100f;
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x5, y5),
                        new Vector2(x8, y6), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x5, y7),
                        new Vector2(x6, y8), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x7, y7),
                        new Vector2(x8, y8), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    var color3 = color * new Color(num, num, num, 1f);
                    flatBatch2D.QueueQuad(new Vector2(x5, y6), new Vector2(x8, y7), 0f, color3);
                    flatBatch2D.QueueQuad(new Vector2(x6, y7), new Vector2(x7, y8), 0f, color3);
                }

                if (SettingsManager.ScreenLayout3 == ScreenLayout.TripleEven)
                {
                    var x9 = -100f;
                    var x10 = ActualSize.X / 2f - _spacing / 2f + _bevel;
                    var x11 = ActualSize.X / 2f + _spacing / 2f - _bevel;
                    var x12 = ActualSize.X + 100f;
                    var x13 = ActualSize.X / 4f;
                    var x14 = ActualSize.X * 3f / 4f;
                    var y9 = -100f;
                    var y10 = ActualSize.Y / 2f - _spacing / 2f + _bevel;
                    var y11 = ActualSize.Y / 2f + _spacing / 2f - _bevel;
                    var y12 = ActualSize.Y + 100f;
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x9, y9),
                        new Vector2(x10, y10), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x11, y9),
                        new Vector2(x12, y10), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x13, y11),
                        new Vector2(x14, y12), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                        directionalLight, 0f);
                    var color4 = color * new Color(num, num, num, 1f);
                    flatBatch2D.QueueQuad(new Vector2(x10, y9), new Vector2(x11, y10), 0f, color4);
                    flatBatch2D.QueueQuad(new Vector2(x9, y10), new Vector2(x12, y11), 0f, color4);
                    flatBatch2D.QueueQuad(new Vector2(x9, y11), new Vector2(x13, y12), 0f, color4);
                    flatBatch2D.QueueQuad(new Vector2(x14, y11), new Vector2(x12, y12), 0f, color4);
                }
            }
            else if (Children.Count == 4)
            {
                var x15 = -100f;
                var x16 = ActualSize.X / 2f - _spacing / 2f + _bevel;
                var x17 = ActualSize.X / 2f + _spacing / 2f - _bevel;
                var x18 = ActualSize.X + 100f;
                var y13 = -100f;
                var y14 = ActualSize.Y / 2f - _spacing / 2f + _bevel;
                var y15 = ActualSize.Y / 2f + _spacing / 2f - _bevel;
                var y16 = ActualSize.Y + 100f;
                BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x15, y13),
                    new Vector2(x16, y14), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                    directionalLight, 0f);
                BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x17, y13),
                    new Vector2(x18, y14), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                    directionalLight, 0f);
                BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x15, y15),
                    new Vector2(x16, y16), 0f, 0f - _bevel, Color.Transparent, color, Color.Transparent, num,
                    directionalLight, 0f);
                BevelledRectangleWidget.QueueBevelledRectangle(null, flatBatch2D, new Vector2(x17, y15),
                    new Vector2(x18, y16), 0f, 0f - _bevel, Children.Count == 3 ? color : Color.Transparent, color,
                    Color.Transparent, num, directionalLight, 0f);
                var color5 = color * new Color(num, num, num, 1f);
                flatBatch2D.QueueQuad(new Vector2(x16, y13), new Vector2(x17, y16), 0f, color5);
                flatBatch2D.QueueQuad(new Vector2(x15, y14), new Vector2(x18, y15), 0f, color5);
            }
        }

        flatBatch2D.TransformTriangles(GlobalTransform, count);
    }
}
