using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public sealed class SubsystemWeaponPresentation : Subsystem
{
    private SubsystemBowBlockBehavior _bow = null!;

    private SubsystemCrossbowBlockBehavior _crossbow = null!;

    private SubsystemMusketBlockBehavior _musket = null!;

    private SubsystemThrowableBlockBehavior _throwable = null!;

    private SubsystemAudio _audio = null!;

    private SubsystemParticles _particles = null!;

    private SubsystemTerrain _terrain = null!;

    private bool _disposed;

    public override void Load(ValuesDictionary valuesDictionary)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _bow = Project.FindSubsystem<SubsystemBowBlockBehavior>(true)!;
        _crossbow = Project.FindSubsystem<SubsystemCrossbowBlockBehavior>(true)!;
        _musket = Project.FindSubsystem<SubsystemMusketBlockBehavior>(true)!;
        _throwable = Project.FindSubsystem<SubsystemThrowableBlockBehavior>(true)!;
        _audio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _particles = Project.FindSubsystem<SubsystemParticles>(true)!;
        _terrain = Project.FindSubsystem<SubsystemTerrain>(true)!;
        _bow.AimPresentationRequested += OnAim;
        _crossbow.AimPresentationRequested += OnAim;
        _musket.AimPresentationRequested += OnAim;
        _throwable.AimPresentationRequested += OnAim;
        _bow.SoundRequested += OnSound;
        _crossbow.SoundRequested += OnSound;
        _musket.SoundRequested += OnSound;
        _throwable.SoundRequested += OnSound;
        _bow.InventoryEditRequested += OnEdit;
        _crossbow.InventoryEditRequested += OnEdit;
        _musket.InventoryEditRequested += OnEdit;
        _bow.FeedbackRequested += OnFeedback;
        _crossbow.FeedbackRequested += OnFeedback;
        _musket.FeedbackRequested += OnFeedback;
        _musket.SmokeRequested += OnSmoke;
    }

    private bool IsActive => !_disposed && WorldRuntimeContext.IsProjectActive(Project);

    internal static bool CanPresentCharacter(Project project, ComponentCharacter character)
    {
        return WorldRuntimeContext.IsProjectActive(project) &&
               ReferenceEquals(character.Project, project) &&
               character.IsLocallyControlled;
    }

    internal static bool CanEdit(Project project, WeaponInventoryEditRequest request)
    {
        if (!CanPresentCharacter(project, request.Character) ||
            !ReferenceEquals(request.Inventory.Project, project) ||
            request.SlotIndex < 0 ||
            request.SlotIndex >= request.Inventory.SlotsCount ||
            request.Inventory.GetSlotCount(request.SlotIndex) <= 0)
        {
            return false;
        }

        var expected = request.Kind switch
        {
            WeaponInventoryKind.Bow => 191,
            WeaponInventoryKind.Crossbow => 200,
            WeaponInventoryKind.Musket => 212,
            _ => -1
        };
        return Terrain.ExtractContents(request.Inventory.GetSlotValue(request.SlotIndex)) == expected;
    }

    private void OnAim(WeaponAimRequest request)
    {
        if (!IsActive || !ReferenceEquals(request.Miner.Project, Project)) { return; }

        var firstPerson = request.Miner.Entity.FindComponent<ComponentFirstPersonModel>();
        if (firstPerson is null) { return; }

        request.Miner.ComponentCharacter?.Entity.FindComponent<ComponentAimingSights>(true)!
            .ShowAimingSights(request.Aim.Position, request.Aim.Direction);
        firstPerson.ItemOffsetOrder = request.ItemOffset;
        if (request.ItemRotation is { } rotation) { firstPerson.ItemRotationOrder = rotation; }
    }

    private void OnEdit(WeaponInventoryEditRequest request)
    {
        if (!IsActive || !CanEdit(Project, request)) { return; }

        var gui = request.Character.Entity.FindComponent<ComponentGui>(true)!;
        gui.ModalPanelWidget = gui.ModalPanelWidget is not null
            ? null
            : request.Kind switch
            {
                WeaponInventoryKind.Bow => new BowWidget(request.Inventory, request.SlotIndex),
                WeaponInventoryKind.Crossbow => new CrossbowWidget(request.Inventory, request.SlotIndex),
                WeaponInventoryKind.Musket => new MusketWidget(request.Inventory, request.SlotIndex),
                _ => throw new InvalidOperationException("Unknown weapon inventory kind.")
            };
    }

    private void OnFeedback(WeaponFeedbackRequest request)
    {
        if (!IsActive || !CanPresentCharacter(Project, request.Character)) { return; }

        request.Character.Entity.FindComponent<ComponentGui>(true)!
            .DisplaySmallMessage(
                Localization.ClientLanguage.Resolver.Resolve(request.Text),
                Color.White,
                true,
                false
            );
    }

    private void OnSound(WeaponSoundRequest request)
    {
        if (!IsActive) { return; }

        if (request.Delay is { } delay)
        {
            _audio.PlaySound(request.Name, request.Volume, request.Pitch, request.Position, request.MinDistance, delay);
        }
        else
        {
            _audio.PlaySound(request.Name, request.Volume, request.Pitch, request.Position, request.MinDistance, true);
        }
    }

    private void OnSmoke(WeaponSmokeRequest request)
    {
        if (IsActive)
        {
            _particles.AddParticleSystem(new GunSmokeParticleSystem(_terrain, request.Position, request.Direction));
        }
    }

    public override void Dispose()
    {
        if (_disposed) { return; }

        _disposed = true;
        if (_bow is not null)
        {
            _bow.AimPresentationRequested -= OnAim;
            _bow.SoundRequested -= OnSound;
            _bow.InventoryEditRequested -= OnEdit;
            _bow.FeedbackRequested -= OnFeedback;
        }

        if (_crossbow is not null)
        {
            _crossbow.AimPresentationRequested -= OnAim;
            _crossbow.SoundRequested -= OnSound;
            _crossbow.InventoryEditRequested -= OnEdit;
            _crossbow.FeedbackRequested -= OnFeedback;
        }

        if (_musket is not null)
        {
            _musket.AimPresentationRequested -= OnAim;
            _musket.SoundRequested -= OnSound;
            _musket.InventoryEditRequested -= OnEdit;
            _musket.FeedbackRequested -= OnFeedback;
            _musket.SmokeRequested -= OnSmoke;
        }

        if (_throwable is not null)
        {
            _throwable.AimPresentationRequested -= OnAim;
            _throwable.SoundRequested -= OnSound;
        }

        base.Dispose();
    }
}
