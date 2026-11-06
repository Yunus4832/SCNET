namespace Game.World;

public static class ProjectileMotionMath
{
    public static Matrix CreateVelocityAlignment(Vector3 position, Vector3 velocity)
    {
        var matrix = Matrix.Identity;
        matrix.Up = Vector3.Normalize(velocity);
        matrix.Right = Vector3.Normalize(Vector3.Cross(matrix.Up, Vector3.UnitY));
        matrix.Forward = Vector3.Normalize(Vector3.Cross(matrix.Up, matrix.Right));
        matrix.Translation = position;
        return matrix;
    }
}
