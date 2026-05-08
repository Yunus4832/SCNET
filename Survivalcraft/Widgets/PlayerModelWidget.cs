using Engine.Graphics;

namespace Game.Widgets;

public class PlayerModelWidget : CanvasWidget
{
    public enum Shot
    {
        Body,
        Bust
    }

    private CharacterSkinsCache _characterSkinsCache;

    private Vector2? _lastDrag;

    private readonly ModelWidget _modelWidget;

    private readonly CharacterSkinsCache _publicCharacterSkinsCache;

    private float _rotation;

    public override bool IsHitTestVisible { get; set; } = false;

    public CharacterSkinsCache CharacterSkinsCache
    {
        get => _characterSkinsCache;
        set
        {
            _publicCharacterSkinsCache.Clear();
            _characterSkinsCache = value;
        }
    }

    public Shot CameraShot { get; set; }

    public int AnimateHeadSeed { get; set; }

    public int AnimateHandsSeed { get; set; }

    public bool OuterClothing { get; set; }

    public PlayerClass PlayerClass { get; set; }

    public string CharacterSkinName { get; set; } = string.Empty;

    public Texture2D CharacterSkinTexture
    {
        get => field is not null
            ? field
            : throw new InvalidOperationException("CharacterSkinTexture is not initialized");
        set;
    } = null!;

    public Texture2D OuterClothingTexture
    {
        get => field is not null
            ? field
            : throw new InvalidOperationException("OuterClothingTexture is not initialized");
        set;
    } = null!;


    public PlayerModelWidget()
    {
        _modelWidget = new ModelWidget
        {
            UseAlphaThreshold = true,
            IsPerspective = true
        };
        Children.Add(_modelWidget);
        _publicCharacterSkinsCache = new CharacterSkinsCache();
        _characterSkinsCache = _publicCharacterSkinsCache;
    }

    public override void Update()
    {
        if (Input.Press.HasValue)
        {
            if (_lastDrag.HasValue)
            {
                _rotation += 0.01f * (Input.Press.Value.X - _lastDrag.Value.X);
                _lastDrag = Input.Press.Value;
                Input.Clear();
            }
            else if (HitTestGlobal(Input.Press.Value) == this)
            {
                _lastDrag = Input.Press.Value;
            }
        }
        else
        {
            _lastDrag = null;
            _rotation = MathUtils.NormalizeAngle(_rotation);
            if (MathUtils.Abs(_rotation) > 0.01f)
            {
                _rotation *= MathUtils.PowSign(0.1f, Time.FrameDuration);
            }
            else
            {
                _rotation = 0f;
            }
        }

        _modelWidget.ModelMatrix = _rotation != 0f ? Matrix.CreateRotationY(_rotation) : Matrix.Identity;
    }

    protected override void MeasureOverride(Vector2 parentAvailableSize)
    {
        _modelWidget.Model = OuterClothing
            ? CharacterSkinsManager.GetOuterClothingModel(PlayerClass)
            : CharacterSkinsManager.GetPlayerModel(PlayerClass);
        if (CameraShot == Shot.Body)
        {
            _modelWidget.ViewPosition = PlayerClass == PlayerClass.Male
                ? new Vector3(0f, 1.46f, -3.2f)
                : new Vector3(0f, 1.39f, -3.04f);
            _modelWidget.ViewTarget =
                PlayerClass == PlayerClass.Male ? new Vector3(0f, 0.9f, 0f) : new Vector3(0f, 0.86f, 0f);
            _modelWidget.ViewFov = 0.57f;
        }
        else
        {
            if (CameraShot != Shot.Bust)
            {
                throw new InvalidOperationException("Unknown shot.");
            }

            _modelWidget.ViewPosition = PlayerClass == PlayerClass.Male
                ? new Vector3(0f, 1.5f, -1.05f)
                : new Vector3(0f, 1.43f, -1f);
            _modelWidget.ViewTarget =
                PlayerClass == PlayerClass.Male ? new Vector3(0f, 1.5f, 0f) : new Vector3(0f, 1.43f, 0f);
            _modelWidget.ViewFov = 0.57f;
        }

        _modelWidget.TextureOverride = OuterClothing
            ? OuterClothingTexture
            : !string.IsNullOrEmpty(CharacterSkinName)
                ? CharacterSkinsCache.GetTexture(CharacterSkinName)
                : CharacterSkinTexture;
        if (AnimateHeadSeed != 0)
        {
            var num = AnimateHeadSeed < 0 ? GetHashCode() : AnimateHeadSeed;
            var num2 = (float)MathUtils.Remainder(Time.FrameStartTime + 1000.0 * num, 10000.0);
            Vector2 vector = default;
            vector.X = MathUtils.Lerp(-0.75f, 0.75f, SimplexNoise.OctavedNoise(num2 + 100f, 0.2f, 1, 2f, 0.5f));
            vector.Y = MathUtils.Lerp(-0.5f, 0.5f, SimplexNoise.OctavedNoise(num2 + 200f, 0.17f, 1, 2f, 0.5f));
            var value = Matrix.CreateRotationX(vector.Y) * Matrix.CreateRotationZ(vector.X);
            _modelWidget.SetBoneTransform(_modelWidget.Model.FindBone("Head")!.Index, value);
        }

        if (!OuterClothing && AnimateHandsSeed != 0)
        {
            var num3 = AnimateHandsSeed < 0 ? GetHashCode() : AnimateHandsSeed;
            var num4 = (float)MathUtils.Remainder(Time.FrameStartTime + 1000.0 * num3, 10000.0);
            Vector2 vector2 = default;
            vector2.X = MathUtils.Lerp(0.2f, 0f, SimplexNoise.OctavedNoise(num4 + 100f, 0.7f, 1, 2f, 0.5f));
            vector2.Y = MathUtils.Lerp(-0.3f, 0.3f, SimplexNoise.OctavedNoise(num4 + 200f, 0.7f, 1, 2f, 0.5f));
            Vector2 vector3 = default;
            vector3.X = MathUtils.Lerp(-0.2f, 0f, SimplexNoise.OctavedNoise(num4 + 300f, 0.7f, 1, 2f, 0.5f));
            vector3.Y = MathUtils.Lerp(-0.3f, 0.3f, SimplexNoise.OctavedNoise(num4 + 400f, 0.7f, 1, 2f, 0.5f));
            var value2 = Matrix.CreateRotationX(vector2.Y) * Matrix.CreateRotationY(vector2.X);
            var value3 = Matrix.CreateRotationX(vector3.Y) * Matrix.CreateRotationY(vector3.X);
            _modelWidget.SetBoneTransform(_modelWidget.Model.FindBone("Hand1")!.Index, value2);
            _modelWidget.SetBoneTransform(_modelWidget.Model.FindBone("Hand2")!.Index, value3);
        }

        base.MeasureOverride(parentAvailableSize);
    }
}
