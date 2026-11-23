using Engine.Graphics;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemParticles : Subsystem, IDrawable, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Presentation;

    private readonly int[] _drawOrders = [300];

    private readonly List<ParticleSystemBase> _endedParticleSystems = [];

    private readonly Dictionary<ParticleSystemBase, int> _particleSystems = new();

    internal const int burstParticleLimit = 1800;

    private int _reservedBurstParticles;

    private SubsystemTime _subsystemTime = null!;

    private bool _particleSystemsDraw = true;

    private bool _particleSystemsSimulate = true;

    public PrimitivesRenderer3D PrimitivesRenderer => field ??= new PrimitivesRenderer3D();

    private SubsystemSkyPresentation SubsystemSkyPresentation { get; set; } = null!;

    public int[] DrawOrders => _drawOrders;

    public void Draw(Camera camera, int drawOrder)
    {
        if (!_particleSystemsDraw)
        {
            return;
        }

        foreach (var key in _particleSystems.Keys)
        {
            key.Draw(camera);
        }

        var shader = ContentManager.Get<Shader>("Shaders/AlphaTested");
        shader.GetParameter("u_origin").SetValue(Vector2.Zero);
        shader.GetParameter("u_viewProjectionMatrix").SetValue(camera.ViewProjectionMatrix);
        shader.GetParameter("u_viewPosition").SetValue(camera.ViewPosition);
        SubsystemSkyPresentation.BindAtmosphere(shader, camera);
        shader.GetParameter("u_alphaThreshold").SetValue(0f);
        var parameter = shader.GetParameter("u_texture");
        var parameter2 = shader.GetParameter("u_samplerState");
        foreach (var texturedBatch in PrimitivesRenderer.TexturedBatches)
        {
            shader.GetParameter("u_fogColorContribution").SetValue(
                ReferenceEquals(texturedBatch.BlendState, BlendState.PremultipliedAdditive) ? 0f : 1f
            );
            Display.DepthStencilState = texturedBatch.DepthStencilState;
            Display.RasterizerState = texturedBatch.RasterizerState;
            Display.BlendState = texturedBatch.BlendState;
            parameter.SetValue(texturedBatch.Texture);
            parameter2.SetValue(texturedBatch.SamplerState);
            texturedBatch.FlushWithDeviceState(shader);
        }

        PrimitivesRenderer.Flush(camera.ViewProjectionMatrix);
    }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (_particleSystemsSimulate)
        {
            _endedParticleSystems.Clear();
            foreach (var key in _particleSystems.Keys)
            {
                if (key.Simulate(_subsystemTime.GameTimeDelta))
                {
                    _endedParticleSystems.Add(key);
                }
            }

            foreach (var endedParticleSystem in _endedParticleSystems)
            {
                RemoveParticleSystem(endedParticleSystem);
            }
        }
    }

    public void AddParticleSystem(ParticleSystemBase particleSystem)
    {
        AddNetParticleSystem(particleSystem);
    }

    /// <summary>为短时特效预留粒子槽位，超额时跳过构造，系统移除时归还槽位。</summary>
    internal bool TryAddBurst(int particleCount, Func<ParticleSystemBase> create)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(particleCount);
        if (!WorldRuntimeContext.For(Project).HasPresentation ||
            particleCount > burstParticleLimit - _reservedBurstParticles)
        {
            return false;
        }

        Register(create(), particleCount);
        return true;
    }

    public void AddNetParticleSystem(ParticleSystemBase particleSystem)
    {
        if (WorldRuntimeContext.For(Project).HasPresentation) { Register(particleSystem, 0); }
    }

    private void Register(ParticleSystemBase particleSystem, int particleCount)
    {
        if (particleSystem.SubsystemParticles == null)
        {
            _particleSystems.Add(particleSystem, particleCount);
            _reservedBurstParticles += particleCount;
            particleSystem.SubsystemParticles = this;
            try { particleSystem.OnAdded(); }
            catch
            {
                if (_particleSystems.Remove(particleSystem, out var reserved))
                {
                    _reservedBurstParticles -= reserved;
                    particleSystem.SubsystemParticles = null;
                }

                throw;
            }

            return;
        }

        throw new InvalidOperationException("Particle system is already added.");
    }

    public void RemoveParticleSystem(ParticleSystemBase particleSystem)
    {
        RemoveNetParticleSystem(particleSystem);
    }

    private void RemoveNetParticleSystem(ParticleSystemBase particleSystem)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation)
        {
            return;
        }

        if (particleSystem.SubsystemParticles != this)
        {
            throw new InvalidOperationException("Particle system is not added.");
        }

        particleSystem.OnRemoved();
        _particleSystems.Remove(particleSystem, out var reserved);
        _reservedBurstParticles -= reserved;
        particleSystem.SubsystemParticles = null;
    }

    public bool ContainsParticleSystem(ParticleSystemBase? particleSystem)
    {
        return particleSystem?.SubsystemParticles == this;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        if (!WorldRuntimeContext.For(Project).HasPresentation) { return; }

        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        SubsystemSkyPresentation = Project.FindSubsystem<SubsystemSkyPresentation>(true)!;
    }
}
