using Engine.Graphics;

namespace Game.ParticleSystems;

public class FireworksParticleSystem : ParticleSystem<FireworksParticleSystem.Particle>
{
    private readonly Color _color;

    private readonly float _flickering;

    private int _nextParticle;

    private readonly Random _random = new();

    public FireworksParticleSystem(
        Vector3 position,
        Color color,
        FireworksBlock.Shape shape,
        float flickering,
        float particleSize
    ) : base(300)
    {
        Texture = AssetManager.Get<Texture2D>("Textures/FireworksParticle");
        _color = color;
        _flickering = flickering;
        TextureSlotsCount = 2;
        if (shape is FireworksBlock.Shape.SmallBurst or FireworksBlock.Shape.LargeBurst)
        {
            var burstParticleCount = shape == FireworksBlock.Shape.SmallBurst ? 100 : 200;
            while (_nextParticle < burstParticleCount)
            {
                var particle = Particles[_nextParticle++];
                particle.IsActive = true;
                particle.Position = position;
                particle.Size = new Vector2(0.2f * particleSize);
                particle.TimeToLive = shape == FireworksBlock.Shape.SmallBurst
                    ? _random.Float(0.5f, 2f)
                    : _random.Float(1f, 3f);
                particle.Velocity = _random.Vector3(0.5f, 1f);
                particle.Velocity *= (shape == FireworksBlock.Shape.SmallBurst ? 16 : 26) *
                                     particle.Velocity.LengthSquared();
                particle.TextureSlot = _random.Int(0, 3);
                particle.FadeRate = _random.Float(1f, 3f);
                particle.BaseColor = _color * _random.Float(0.5f, 1f);
                particle.RotationSpeed = 0f;
            }
        }

        switch (shape)
        {
            case FireworksBlock.Shape.Circle:
            {
                var angleOffset = _random.Float(0f, (float)Math.PI * 2f);
                var ringParticleCount = 150;
                for (var j = 0; j < ringParticleCount; j++)
                {
                    var x2 = (float)Math.PI * 2f * j / ringParticleCount + angleOffset;
                    var v2 = new Vector3(
                        MathUtils.Sin(x2) + 0.1f * _random.Float(-1f, 1f),
                        0f,
                        MathUtils.Cos(x2) + 0.1f * _random.Float(-1f, 1f)
                    );
                    var obj2 = Particles[_nextParticle++];
                    obj2.IsActive = true;
                    obj2.Position = position;
                    obj2.Size = new Vector2(0.2f * particleSize);
                    obj2.TimeToLive = _random.Float(1f, 3f);
                    obj2.Velocity = 20f * v2;
                    obj2.TextureSlot = _random.Int(0, 3);
                    obj2.FadeRate = _random.Float(1f, 3f);
                    obj2.BaseColor = _color * _random.Float(0.5f, 1f);
                    obj2.RotationSpeed = 0f;
                }

                break;
            }
            case FireworksBlock.Shape.Disc:
            {
                var angleOffset = _random.Float(0f, (float)Math.PI * 2f);
                var radialBands = 13;
                for (var m = 0; m <= radialBands; m++)
                {
                    var radiusFraction = m / (float)radialBands;
                    var particlesInRing = (int)MathUtils.Round(radiusFraction * 2f * radialBands);
                    for (var n = 0; n < particlesInRing; n++)
                    {
                        var x5 = (float)Math.PI * 2f * n / particlesInRing + angleOffset;
                        var v4 = new Vector3(
                            radiusFraction * MathUtils.Sin(x5) + 0.1f * _random.Float(-1f, 1f),
                            0f,
                            radiusFraction * MathUtils.Cos(x5) + 0.1f * _random.Float(-1f, 1f)
                        );
                        var obj4 = Particles[_nextParticle++];
                        obj4.IsActive = true;
                        obj4.Position = position;
                        obj4.Size = new Vector2(0.2f * particleSize);
                        obj4.TimeToLive = _random.Float(1f, 3f);
                        obj4.Velocity = 22f * v4;
                        obj4.TextureSlot = _random.Int(0, 3);
                        obj4.FadeRate = _random.Float(1f, 3f);
                        obj4.BaseColor = _color * _random.Float(0.5f, 1f);
                        obj4.RotationSpeed = 0f;
                    }
                }

                break;
            }
            case FireworksBlock.Shape.Ball:
            {
                var angleOffset = _random.Float(0f, (float)Math.PI * 2f);
                var latitudeBands = 12;
                Vector3 v5 = default;
                for (var latitudeBand = 0; latitudeBand <= latitudeBands; latitudeBand++)
                {
                    var x6 = (float)Math.PI * latitudeBand / latitudeBands;
                    v5.Y = MathUtils.Cos(x6);
                    var latitudeRadius = MathUtils.Sin(x6);
                    var particlesInBand = (int)MathUtils.Round(latitudeRadius * 2f * latitudeBands);
                    for (var longitudeIndex = 0; longitudeIndex < particlesInBand; longitudeIndex++)
                    {
                        var x7 = (float)Math.PI * 2f * longitudeIndex / particlesInBand + angleOffset;
                        v5.X = latitudeRadius * MathUtils.Sin(x7);
                        v5.Z = latitudeRadius * MathUtils.Cos(x7);
                        var obj5 = Particles[_nextParticle++];
                        obj5.IsActive = true;
                        obj5.Position = position;
                        obj5.Size = new Vector2(0.2f * particleSize);
                        obj5.TimeToLive = _random.Float(1f, 3f);
                        obj5.Velocity = 20f * v5;
                        obj5.TextureSlot = _random.Int(0, 3);
                        obj5.FadeRate = _random.Float(1f, 3f);
                        obj5.BaseColor = _color * _random.Float(0.5f, 1f);
                        obj5.RotationSpeed = 0f;
                    }
                }

                break;
            }
            case FireworksBlock.Shape.ShortTrails:
            case FireworksBlock.Shape.LongTrails:
            {
                var angleOffset = _random.Float(0f, (float)Math.PI * 2f);
                var latitudeBands = 3;
                Vector3 v3 = default;
                for (var k = 0; k <= latitudeBands; k++)
                {
                    var x3 = (float)Math.PI * k / latitudeBands;
                    var latitudeRadius = MathUtils.Sin(x3);
                    var particlesInBand = (int)MathUtils.Round(
                        latitudeRadius * (shape == FireworksBlock.Shape.ShortTrails ? 3 : 2) * latitudeBands
                    );
                    for (var l = 0; l < particlesInBand; l++)
                    {
                        var x4 = (float)Math.PI * 2f * l / particlesInBand + angleOffset;
                        v3.X = latitudeRadius * MathUtils.Sin(x4) + 0.3f * _random.Float(-1f, 1f);
                        v3.Y = MathUtils.Cos(x3) + 0.3f * _random.Float(-1f, 1f);
                        v3.Z = latitudeRadius * MathUtils.Cos(x4) + 0.3f * _random.Float(-1f, 1f);
                        var obj3 = Particles[_nextParticle++];
                        obj3.IsActive = true;
                        obj3.Position = position;
                        obj3.Size = new Vector2(0.25f);
                        obj3.TimeToLive = _random.Float(0.5f, 2.5f);
                        obj3.Velocity = shape == FireworksBlock.Shape.ShortTrails ? 25f * v3 : 35f * v3;
                        obj3.TextureSlot = _random.Int(0, 3);
                        obj3.FadeRate = _random.Float(1f, 3f);
                        obj3.BaseColor = _color * _random.Float(0.5f, 1f);
                        obj3.GenerationFrequency = shape == FireworksBlock.Shape.ShortTrails ? 1.9f : 2.1f;
                        obj3.RotationSpeed = _random.Float(-40f, 40f);
                    }
                }

                break;
            }
            case FireworksBlock.Shape.FlatTrails:
            {
                var angleOffset = _random.Float(0f, (float)Math.PI * 2f);
                var ringParticleCount = 13;
                for (var i = 0; i < ringParticleCount; i++)
                {
                    var x = (float)Math.PI * 2f * i / ringParticleCount + angleOffset;
                    var v = new Vector3(
                        MathUtils.Sin(x) + 0.1f * _random.Float(-1f, 1f),
                        0f,
                        MathUtils.Cos(x) + 0.1f * _random.Float(-1f, 1f)
                    );
                    var obj = Particles[_nextParticle++];
                    obj.IsActive = true;
                    obj.Position = position;
                    obj.Size = new Vector2(0.25f);
                    obj.TimeToLive = _random.Float(0.5f, 2.5f);
                    obj.Velocity = 25f * v;
                    obj.TextureSlot = _random.Int(0, 3);
                    obj.FadeRate = _random.Float(1f, 3f);
                    obj.BaseColor = _color * _random.Float(0.5f, 1f);
                    obj.GenerationFrequency = 2.5f;
                    obj.RotationSpeed = _random.Float(-40f, 40f);
                }

                break;
            }
        }
    }

    public override bool Simulate(float dt)
    {
        dt = MathUtils.Clamp(dt, 0f, 0.1f);
        var strongDamping = MathUtils.Pow(0.01f, dt);
        var normalDamping = MathUtils.Pow(0.1f, dt);
        var hasActiveParticles = false;
        for (var i = 0; i < Particles.Length; i++)
        {
            var particle = Particles[i];
            if (!particle.IsActive)
            {
                continue;
            }

            hasActiveParticles = true;
            particle.TimeToLive -= dt;
            if (particle.TimeToLive > 0f)
            {
                var position = particle.Position += particle.Velocity * dt;
                particle.Velocity.Y += -9.81f * dt;
                particle.Velocity *= particle.HighDamping ? strongDamping : normalDamping;
                particle.Color = particle.BaseColor * MathUtils.Min(particle.FadeRate * particle.TimeToLive, 1f);
                particle.Rotation += particle.RotationSpeed * dt;
                if (!particle.HighDamping && _random.Float(0f, 1f) < _flickering)
                {
                    particle.Color = Color.Transparent;
                }

                if (_random.Float(0f, 1f) < 20f * dt)
                {
                    particle.TextureSlot = _random.Int(0, 3);
                }

                if (!(particle.GenerationFrequency > 0f))
                {
                    continue;
                }

                var speed = particle.Velocity.Length();
                particle.GenerationAccumulator += particle.GenerationFrequency * speed * dt;
                if (!(particle.GenerationAccumulator > 1f) || _nextParticle >= Particles.Length)
                {
                    continue;
                }

                particle.GenerationAccumulator -= 1f;
                var obj = Particles[_nextParticle++];
                obj.IsActive = true;
                obj.Position = position;
                obj.Size = new Vector2(0.2f);
                obj.TimeToLive = 1f;
                obj.TextureSlot = _random.Int(0, 3);
                obj.FadeRate = 1f;
                obj.BaseColor = particle.BaseColor;
                obj.HighDamping = true;
                obj.RotationSpeed = 0f;
            }
            else
            {
                particle.IsActive = false;
            }
        }

        return !hasActiveParticles;
    }

    public class Particle : Game.ParticleSystems.Particle
    {
        public Color BaseColor;

        public float FadeRate;

        public float GenerationAccumulator;

        public float GenerationFrequency;

        public bool HighDamping;

        public float RotationSpeed;

        public float TimeToLive;

        public Vector3 Velocity;
    }
}
