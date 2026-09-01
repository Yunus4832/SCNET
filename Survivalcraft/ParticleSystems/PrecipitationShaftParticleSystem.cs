using Engine.Graphics;

namespace Game.ParticleSystems;

public class PrecipitationShaftParticleSystem : ParticleSystemBase
{
    public const float ViewHeight = 10f;

    public const int ParticlesCount = 4;

    private float _averageSpeed;

    private TexturedBatch3D? _batch;

    private readonly GameWidget _gameWidget;

    private float _intensity;

    private bool _isEmpty;

    private bool _isVisible;

    private float _lastSkylightIntensity = -3.40282347E+38f;

    private double _lastUpdateTime = -1.7976931348623157E+308;

    private float? _lastViewY;

    private bool _needsInitialize;

    private readonly Particle[] _particles = new Particle[4];

    private readonly PrecipitationType _precipitationType;

    private readonly Random _random;

    private Vector2 _size;

    private readonly SubsystemWeather _subsystemWeather;

    private Texture2D Texture
    {
        get => field is not null ? field : throw new InvalidOperationException("Texture is not initialized");
        set;
    } = null!;

    private float _toCreate;

    private int _topmostBelowValue;

    private int _topmostValue;

    private int _yLimit;

    public PrecipitationShaftParticleSystem(
        GameWidget gameWidget,
        SubsystemWeather subsystemWeather,
        Random random,
        Point2 point,
        PrecipitationType precipitationType
    )
    {
        _gameWidget = gameWidget;
        _subsystemWeather = subsystemWeather;
        _random = random;
        Point = point;
        _precipitationType = precipitationType;
        for (var i = 0; i < _particles.Length; i++)
        {
            _particles[i] = new Particle();
        }

        Initialize();
    }

    public Point2 Point { get; set; }

    public override bool Simulate(float dt)
    {
        if (_subsystemWeather.SubsystemTime.GameTime - _lastUpdateTime > 1.0 ||
            MathUtils.Abs(_lastSkylightIntensity - _subsystemWeather.SubsystemSky.SkyLightIntensity) > 0.1f)
        {
            _lastUpdateTime = _subsystemWeather.SubsystemTime.GameTime;
            _lastSkylightIntensity = _subsystemWeather.SubsystemSky.SkyLightIntensity;
            var precipitationShaftInfo = _subsystemWeather.GetPrecipitationShaftInfo(Point.X, Point.Y);
            _intensity = precipitationShaftInfo.Intensity;
            _yLimit = precipitationShaftInfo.YLimit;
            _topmostValue =
                _subsystemWeather.SubsystemTerrain.Terrain.GetCellValue(Point.X, precipitationShaftInfo.YLimit - 1,
                    Point.Y);
            _topmostBelowValue =
                _subsystemWeather.SubsystemTerrain.Terrain.GetCellValue(Point.X, precipitationShaftInfo.YLimit - 2,
                    Point.Y);
        }

        var activeCamera = _gameWidget.ActiveCamera;
        if (_isEmpty && (!(_intensity > 0f) || !(_yLimit < activeCamera.ViewPosition.Y + 5f)))
        {
            return false;
        }

        var v = Vector2.Normalize(new Vector2(activeCamera.ViewDirection.X, activeCamera.ViewDirection.Z));
        var v2 = Vector2.Normalize(new Vector2(Point.X + 0.5f - activeCamera.ViewPosition.X + 0.7f * v.X,
            Point.Y + 0.5f - activeCamera.ViewPosition.Z + 0.7f * v.Y));
        var num = Vector2.Dot(v, v2);
        _isVisible = num > 0.5f;
        if (_isVisible)
        {
            if (_needsInitialize)
            {
                _needsInitialize = false;
                Initialize();
            }

            var y = activeCamera.ViewPosition.Y;
            var num2 = y - 5f;
            var num3 = y + 5f;
            float num4;
            float num5;
            if (_lastViewY.HasValue)
            {
                if (y < _lastViewY.Value)
                {
                    num4 = num2;
                    num5 = _lastViewY.Value - 5f;
                }
                else
                {
                    num4 = _lastViewY.Value + 5f;
                    num5 = num3;
                }
            }
            else
            {
                num4 = num2;
                num5 = num3;
            }

            var num6 = (num5 - num4) / 10f * _particles.Length * _intensity;
            var num7 = (int)num6 + (_random.Float(0f, 1f) < num6 - (int)num6 ? 1 : 0);
            _lastViewY = y;
            _toCreate += _particles.Length * _intensity / 10f * _averageSpeed * dt;
            _isEmpty = true;
            var num8 = _precipitationType == PrecipitationType.Rain ? 0f : 0.03f;
            foreach (var particle in _particles)
            {
                if (particle.IsActive)
                {
                    if (particle.YLimit == 0f && particle.Position.Y <= _yLimit + num8)
                    {
                        RaycastParticle(particle);
                    }

                    var flag = particle.YLimit != 0f && particle.Position.Y <= particle.YLimit + num8;
                    if (!flag && particle.Position.Y >= num2 && particle.Position.Y <= num3)
                    {
                        particle.Position.Y -= particle.Speed * dt;
                        _isEmpty = false;
                        continue;
                    }

                    particle.IsActive = false;
                    if (!particle.GenerateSplash || !flag)
                    {
                        continue;
                    }

                    if (_precipitationType == PrecipitationType.Rain && _random.Bool(0.5f))
                    {
                        _subsystemWeather.RainSplashParticleSystem.AddSplash(_topmostValue,
                            new Vector3(particle.Position.X, particle.YLimit + num8, particle.Position.Z),
                            _subsystemWeather.RainColor);
                    }

                    if (_precipitationType == PrecipitationType.Snow)
                    {
                        _subsystemWeather.SnowSplashParticleSystem.AddSplash(_topmostValue,
                            new Vector3(particle.Position.X, particle.YLimit + num8, particle.Position.Z),
                            _size, _subsystemWeather.SnowColor, particle.TextureSlot);
                    }
                }
                else if (num7 > 0)
                {
                    particle.Position.X = Point.X + _random.Float(0f, 1f);
                    particle.Position.Y = _random.Float(num4, num5);
                    particle.Position.Z = Point.Y + _random.Float(0f, 1f);
                    particle.IsActive = particle.Position.Y >= _yLimit;
                    particle.YLimit = 0f;
                    num7--;
                }
                else if (_toCreate >= 1f)
                {
                    particle.Position.X = Point.X + _random.Float(0f, 1f);
                    particle.Position.Y = _random.Float(num3 - _averageSpeed * dt, num3);
                    particle.Position.Z = Point.Y + _random.Float(0f, 1f);
                    particle.IsActive = particle.Position.Y >= _yLimit;
                    particle.YLimit = 0f;
                    _toCreate -= 1f;
                }
            }

            _toCreate -= MathUtils.Floor(_toCreate);
        }
        else
        {
            _needsInitialize = true;
        }

        return false;
    }

    public override void Draw(Camera camera)
    {
        if (SubsystemParticles is null)
        {
            throw new InvalidOperationException("SubsystemParticles is not initialized");
        }

        if (!_isVisible || _isEmpty || camera.GameWidget != _gameWidget)
        {
            return;
        }

        _batch ??= SubsystemParticles.PrimitivesRenderer.TexturedBatch(
            Texture,
            false,
            0,
            DepthStencilState.DepthRead,
            null,
            BlendState.AlphaBlend,
            SamplerState.PointClamp
        );
        var num = camera.ViewPosition.Y + 5f;
        var viewDirection = camera.ViewDirection;
        var vector = Vector3.Normalize(Vector3.Cross(viewDirection, Vector3.UnitY));
        var v = _precipitationType == PrecipitationType.Rain
            ? Vector3.UnitY
            : Vector3.Normalize(Vector3.Cross(viewDirection, vector));
        var vector2 = vector * _size.X;
        var vector3 = v * _size.Y;
        if (_precipitationType == PrecipitationType.Rain)
        {
            var v2 = -vector2 - vector3;
            var v3 = vector2 - vector3;
            var v4 = vector3;
            foreach (var particle in _particles)
            {
                if (!particle.IsActive)
                {
                    continue;
                }

                var p = particle.Position + v2;
                var p2 = particle.Position + v3;
                var p3 = particle.Position + v4;
                var color = _subsystemWeather.RainColor * MathUtils.Min(0.6f * (num - particle.Position.Y), 1f);
                _batch.QueueTriangle(p, p2, p3, particle.TexCoord1, particle.TexCoord2, particle.TexCoord3, color);
            }

            return;
        }

        var v5 = -vector2 - vector3;
        var v6 = vector2 - vector3;
        var v7 = vector2 + vector3;
        var v8 = -vector2 + vector3;
        foreach (var particle2 in _particles)
        {
            if (!particle2.IsActive)
            {
                continue;
            }

            var p4 = particle2.Position + v5;
            var p5 = particle2.Position + v6;
            var p6 = particle2.Position + v7;
            var p7 = particle2.Position + v8;
            var color2 = _subsystemWeather.SnowColor * MathUtils.Min(0.6f * (num - particle2.Position.Y), 1f);
            _batch.QueueQuad(p4, p5, p6, p7, particle2.TexCoord1, particle2.TexCoord2, particle2.TexCoord3,
                particle2.TexCoord4, color2);
        }
    }

    public void RaycastParticle(Particle particle)
    {
        particle.YLimit = _yLimit;
        particle.GenerateSplash = true;
        var block = BlocksManager.Blocks[Terrain.ExtractContents(_topmostValue)];
        if (!block.Transparent)
        {
            return;
        }

        var ray = new Ray3(new Vector3(particle.Position.X - Point.X, 1f, particle.Position.Z - Point.Y),
            -Vector3.UnitY);
        var num = block.Raycast(ray, _subsystemWeather.SubsystemTerrain, _topmostValue, false, out _, out _);
        if (num.HasValue)
        {
            particle.YLimit -= num.Value;
            return;
        }

        particle.YLimit -= 1f;
        if (BlocksManager.Blocks[Terrain.ExtractContents(_topmostBelowValue)]
            .IsFaceTransparent(_subsystemWeather.SubsystemTerrain, 4, _topmostBelowValue))
        {
            particle.GenerateSplash = false;
        }
    }

    public void Initialize()
    {
        _lastViewY = null;
        _toCreate = _random.Float(0f, 0.9f);
        _batch = null;
        _lastSkylightIntensity = -3.40282347E+38f;
        switch (_precipitationType)
        {
            case PrecipitationType.Rain:
            {
                var num4 = 8f;
                var num5 = 12f;
                _averageSpeed = (num4 + num5) / 2f;
                _size = new Vector2(0.02f, 0.15f);
                Texture = ContentManager.Get<Texture2D>("Textures/RainParticle");
                foreach (var obj in _particles)
                {
                    obj.IsActive = false;
                    obj.TexCoord1 = new Vector2(0f, 1f);
                    obj.TexCoord2 = new Vector2(1f, 1f);
                    obj.TexCoord3 = new Vector2(0.5f, 0f);
                    obj.Speed = _random.Float(num4, num5);
                }

                break;
            }
            case PrecipitationType.Snow:
            {
                var num = 0.25f;
                var num2 = 0.5f;
                var num3 = 3f;
                _averageSpeed = (num2 + num3) / 2f;
                _size = new Vector2(0.07f, 0.07f);
                Texture = ContentManager.Get<Texture2D>("Textures/SnowParticle");
                foreach (var particle in _particles)
                {
                    particle.IsActive = false;
                    particle.TextureSlot = (byte)_random.Int(0, 15);
                    var v = new Vector2(particle.TextureSlot % 4, particle.TextureSlot / 4) * num;
                    particle.TexCoord1 = v + new Vector2(0f, 0f);
                    particle.TexCoord2 = v + new Vector2(num, 0f);
                    particle.TexCoord3 = v + new Vector2(num, num);
                    particle.TexCoord4 = v + new Vector2(0f, num);
                    particle.Speed = _random.Float(num2, num3);
                }

                break;
            }
            default:
                throw new InvalidOperationException("Unknown precipitation type.");
        }
    }

    public class Particle
    {
        public bool GenerateSplash;

        public bool IsActive;

        public Vector3 Position;

        public float Speed;

        public Vector2 TexCoord1;

        public Vector2 TexCoord2;

        public Vector2 TexCoord3;

        public Vector2 TexCoord4;

        public byte TextureSlot;

        public float YLimit;
    }
}
