using Engine.Graphics;

namespace Game.Widgets;

public class ModelWidget : Widget
{
    private static LitShader? _shader;

    private static LitShader? _shaderAlpha;

    private Matrix[] _absoluteBoneTransforms = [];

    private Matrix?[] _boneTransforms = [];

    public override bool IsHitTestVisible { get; set; } = false;

    public Vector2 Size { get; set; }

    public Color Color { get; set; }

    public bool UseAlphaThreshold { get; set; }

    public bool IsPerspective { get; set; }

    public Vector3 OrthographicFrustumSize { get; set; }

    public Vector3 ViewPosition { get; set; }

    public Vector3 ViewTarget { get; set; }

    public float ViewFov { get; set; }

    public Matrix ModelMatrix { get; set; } = Matrix.Identity;


    public Vector3 AutoRotationVector { get; set; }

    public Model Model
    {
        get => field is not null ? field : throw new InvalidOperationException("Model is not initialized");
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            _boneTransforms = new Matrix?[field.Bones.Count];
            _absoluteBoneTransforms = new Matrix[field.Bones.Count];
        }
    } = null!;

    public Texture2D TextureOverride
    {
        get => field is not null ? field : throw new InvalidOperationException("TextureOverride is not initialized");
        set;
    } = null!;

    public Matrix? GetBoneTransform(int boneIndex)
    {
        return _boneTransforms[boneIndex];
    }

    public void SetBoneTransform(int boneIndex, Matrix? transformation)
    {
        _boneTransforms[boneIndex] = transformation;
    }


    public ModelWidget()
    {
        Size = new Vector2(float.PositiveInfinity);
        Color = Color.White;
        UseAlphaThreshold = false;
        IsPerspective = true;
        ViewPosition = new Vector3(0f, 0f, -5f);
        ViewTarget = new Vector3(0f, 0f, 0f);
        ViewFov = 1f;
        OrthographicFrustumSize = new Vector3(0f, 10f, 10f);
    }

    public override void Draw(DrawContext dc)
    {
        var litShader = UseAlphaThreshold
            ? (_shaderAlpha ??= new LitShader(1, false, false, true, false, true))
            : (_shader ??= new LitShader(1, false, false, true, false, false));
        litShader.Texture = TextureOverride;
        litShader.SamplerState = SamplerState.PointClamp;
        litShader.MaterialColor = new Vector4(Color * GlobalColorTransform);
        litShader.AmbientLightColor = new Vector3(0.66f, 0.66f, 0.66f);
        litShader.DiffuseLightColor1 = new Vector3(1f, 1f, 1f);
        litShader.LightDirection1 = Vector3.Normalize(new Vector3(1f, 1f, 1f));
        if (UseAlphaThreshold)
        {
            litShader.AlphaThreshold = 0f;
        }

        litShader.Transforms.View = Matrix.CreateLookAt(ViewPosition, ViewTarget, Vector3.UnitY);
        var viewport = Display.Viewport;
        var num = ActualSize.X / ActualSize.Y;
        if (IsPerspective)
        {
            litShader.Transforms.Projection = Matrix.CreatePerspectiveFieldOfView(ViewFov, num, 0.1f, 100f) *
                                              MatrixUtils.CreateScaleTranslation(0.5f * ActualSize.X,
                                                  -0.5f * ActualSize.Y, ActualSize.X / 2f, ActualSize.Y / 2f) *
                                              GlobalTransform * MatrixUtils.CreateScaleTranslation(2f / viewport.Width,
                                                  -2f / viewport.Height, -1f, 1f);
        }
        else
        {
            var orthographicFrustumSize = OrthographicFrustumSize;
            if (orthographicFrustumSize.X < 0f)
            {
                orthographicFrustumSize.X = orthographicFrustumSize.Y / num;
            }
            else if (orthographicFrustumSize.Y < 0f)
            {
                orthographicFrustumSize.Y = orthographicFrustumSize.X * num;
            }

            litShader.Transforms.Projection =
                Matrix.CreateOrthographic(orthographicFrustumSize.X, orthographicFrustumSize.Y, 0f,
                    OrthographicFrustumSize.Z) *
                MatrixUtils.CreateScaleTranslation(0.5f * ActualSize.X, -0.5f * ActualSize.Y, ActualSize.X / 2f,
                    ActualSize.Y / 2f) * GlobalTransform *
                MatrixUtils.CreateScaleTranslation(2f / viewport.Width, -2f / viewport.Height, -1f, 1f);
        }

        Display.DepthStencilState = DepthStencilState.Default;
        Display.BlendState = BlendState.AlphaBlend;
        Display.RasterizerState = RasterizerState.CullNoneScissor;
        ProcessBoneHierarchy(Model.RootBone, Matrix.Identity, _absoluteBoneTransforms);
        var num2 = (float)Time.RealTime + GetHashCode() % 1000 / 100f;
        var m = AutoRotationVector.LengthSquared() > 0f
            ? Matrix.CreateFromAxisAngle(Vector3.Normalize(AutoRotationVector), AutoRotationVector.Length() * num2)
            : Matrix.Identity;
        foreach (var mesh in Model.Meshes)
        {
            if (mesh.ParentBone != null)
            {
                litShader.Transforms.World[0] = _absoluteBoneTransforms[mesh.ParentBone.Index] * ModelMatrix * m;
            }

            foreach (var meshPart in mesh.MeshParts)
            {
                if (meshPart.IndicesCount <= 0)
                {
                    continue;
                }

                Display.DrawIndexed(
                    PrimitiveType.TriangleList,
                    litShader,
                    meshPart.VertexBuffer,
                    meshPart.IndexBuffer,
                    meshPart.StartIndex,
                    meshPart.IndicesCount
                );
            }
        }
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        IsDrawRequired = true;
        DesiredSize = Size;
    }

    public void ProcessBoneHierarchy(ModelBone modelBone, Matrix currentTransform, Matrix[] transforms)
    {
        var m = modelBone.Transform;
        if (_boneTransforms[modelBone.Index].HasValue)
        {
            var translation = m.Translation;
            m.Translation = Vector3.Zero;
            m *= _boneTransforms[modelBone.Index]!.Value;
            m.Translation += translation;
        }

        Matrix.MultiplyRestricted(ref m, ref currentTransform, out transforms[modelBone.Index]);

        foreach (var childBone in modelBone.ReadOnlyChildBones)
        {
            ProcessBoneHierarchy(childBone, transforms[modelBone.Index], transforms);
        }
    }
}
