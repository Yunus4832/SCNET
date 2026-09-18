namespace Game.Widgets;

public class GamesWidget : ContainerWidget
{
    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        base.MeasureOverride(parentAvailableSize);
        IsOverdrawRequired = false;
    }

    public override void ArrangeOverride()
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
            child.LayoutTransform = Matrix.Identity;
        }
    }
}
