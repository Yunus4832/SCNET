namespace Game.Widgets;

public class ClickableWidget : Widget
{
    public Action? OnClick;

    public string? SoundName { get; set; }

    public bool IsPressed { get; set; }

    public bool IsClicked { get; set; }

    public bool IsTapped { get; set; }

    public bool IsChecked { get; set; }

    public bool IsAutoCheckingEnabled { get; set; }

#pragma warning disable CS0067 // Event is never used
    public event Action? ClickAction;
#pragma warning restore CS0067 // Event is never used

    public override void UpdateCeases()
    {
        base.UpdateCeases();
        IsPressed = false;
        IsClicked = false;
        IsTapped = false;
    }

    public override void Update()
    {
        var input = Input;
        IsPressed = false;
        IsTapped = false;
        IsClicked = false;
        if (input.Press.HasValue && HitTestGlobal(input.Press.Value) == this)
        {
            IsPressed = true;
        }

        if (input.Tap.HasValue && HitTestGlobal(input.Tap.Value) == this)
        {
            IsTapped = true;
        }

        if (!input.Click.HasValue || HitTestGlobal(input.Click.Value.Start) != this ||
            HitTestGlobal(input.Click.Value.End) != this)
        {
            return;
        }

        IsClicked = true;
        OnClick?.Invoke();
        if (IsAutoCheckingEnabled)
        {
            IsChecked = !IsChecked;
        }

        if (!string.IsNullOrEmpty(SoundName))
        {
            AudioManager.PlaySound(SoundName, 1f, 0f, 0f);
        }
    }
}
