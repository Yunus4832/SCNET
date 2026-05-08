namespace Game;

public class Pickable : WorldItem
{
    public int Count;

    public float Distance;

    public Vector3? FlyToPosition;

    public byte? GetPickPlayer;

    //联机增加参数
    public ushort Id = 0;

    public Vector3? LastPosition;

    public bool NetToRemove;

    public bool PlayPickupSound = false;

    public bool PlaySound;

    public Vector3 PositionFix;

    public bool SplashGenerated = true;

    public Matrix? StuckMatrix;
}
