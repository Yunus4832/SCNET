namespace Engine.Core.Mathematics;

/// <summary>
///     常量扩展函数
/// </summary>
public static class ConstantsExtension
{
    public static bool CloseTo(this float left, float right)
    {
        // 处理特殊值
        if (float.IsNaN(left) && float.IsNaN(right))
        {
            return true; // NaN 与 NaN 视为相等
        }

        if (float.IsInfinity(left) && float.IsInfinity(right))
        {
            return Math.Sign(left) == Math.Sign(right); // 检查正无穷与正无穷，负无穷与负无穷
        }

        return Math.Abs(left - right) < Constants.Tolerance;
    }

    public static bool UncloseTo(this float left, float right)
    {
        return !left.CloseTo(right);
    }
}
