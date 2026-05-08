namespace Game.Compare;

public class DoubleCompare : IComparer<double>
{
    public int Compare(double x, double y)
    {
        var t = y - x;
        return t > 0 ? 1 : t == 0 ? 0 : -1;
    }
}
