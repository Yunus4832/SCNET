namespace Engine.Input;

public enum CursorType
{
    /// <summary>默认光标。</summary>
    Default = 0,

    /// <summary>标准箭头光标。</summary>
    Arrow = 1,

    /// <summary>文本输入用的工字形光标。</summary>
    IBeam = 2,

    /// <summary>十字准星光标。</summary>
    Crosshair = 3,

    /// <summary>手形光标。</summary>
    Hand = 4,

    /// <summary>水平调整大小光标。</summary>
    HResize = 5,

    /// <summary>垂直调整大小光标。</summary>
    VResize = 6,

    /// <summary>沿左上至右下方向调整大小或移动的光标。</summary>
    NwseResize = 7,

    /// <summary>沿右上至左下方向调整大小或移动的光标。</summary>
    NeswResize = 8,

    /// <summary>全方向调整大小或移动的光标。</summary>
    ResizeAll = 9,

    /// <summary>表示禁止操作的光标。</summary>
    NotAllowed = 10,

    /// <summary>表示等待的沙漏光标。</summary>
    Wait = 11,

    /// <summary>带等待图标的标准箭头光标。</summary>
    WaitArrow = 12,

    Grab = 13,

    Grabbing = 14
}
