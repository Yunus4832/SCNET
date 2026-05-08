using Engine.Core;

namespace Engine.Graphics;

public class ShaderTransforms
{
    private Matrix _projection = Matrix.Identity;

    private Matrix _view = Matrix.Identity;

    private Matrix _viewProjection = Matrix.Identity;

    public ShaderTransforms(int maxWorldMatrices)
    {
        World = new Matrix[maxWorldMatrices];
        WorldView = new Matrix[maxWorldMatrices];
        WorldViewProjection = new Matrix[maxWorldMatrices];
        for (var i = 0; i < maxWorldMatrices; i++)
        {
            World[i] = Matrix.Identity;
            WorldView[i] = Matrix.Identity;
            WorldViewProjection[i] = Matrix.Identity;
        }
    }

    public int MaxWorldMatrices => World.Length;

    public Matrix[] World { get; }

    public Matrix View
    {
        get => _view;
        set => _view = value;
    }

    public Matrix Projection
    {
        get => _projection;
        set => _projection = value;
    }

    public Matrix ViewProjection => _viewProjection;

    public Matrix[] WorldView { get; }

    public Matrix[] WorldViewProjection { get; }

    public void UpdateMatrices(int count, bool worldView, bool viewProjection, bool worldViewProjection)
    {
        if (count < 1 || count > MaxWorldMatrices)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (worldView)
        {
            for (var i = 0; i < count; i++)
            {
                Matrix.MultiplyRestricted(ref World[i], ref _view, out WorldView[i]);
            }
        }

        if (viewProjection)
        {
            Matrix.MultiplyRestricted(ref _view, ref _projection, out _viewProjection);
        }

        if (!worldViewProjection)
        {
            return;
        }

        if (worldView)
        {
            for (var j = 0; j < count; j++)
            {
                Matrix.MultiplyRestricted(ref WorldView[j], ref _projection, out WorldViewProjection[j]);
            }

            return;
        }

        if (!viewProjection)
        {
            Matrix.MultiplyRestricted(ref _view, ref _projection, out _viewProjection);
        }

        for (var k = 0; k < count; k++)
        {
            Matrix.MultiplyRestricted(ref World[k], ref _viewProjection, out WorldViewProjection[k]);
        }
    }
}
