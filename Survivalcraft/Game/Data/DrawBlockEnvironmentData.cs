namespace Game;

public class DrawBlockEnvironmentData
{
    public Vector3? BillboardDirection;

    public DrawBlockMode DrawBlockMode;

    public int Humidity = 15;

    public Matrix InWorldMatrix = Matrix.Identity;

    public int Light = 15;

    public SubsystemTerrain? SubsystemTerrain;

    public int Temperature = 8;

    public Matrix? ViewProjectionMatrix;
}
