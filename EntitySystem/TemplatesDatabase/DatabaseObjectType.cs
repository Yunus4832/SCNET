using Engine.Core;

namespace EntitySystem.TemplatesDatabase;

public class DatabaseObjectType(
    string name,
    string defaultInstanceName,
    string iconName,
    int order,
    bool supportsValue,
    bool mustInherit,
    int nameLengthLimit,
    bool saveStandalone)
{
    private readonly List<DatabaseObjectType> _allowedInheritanceChildren = [];

    private readonly List<DatabaseObjectType> _allowedNestingChildren = [];

    private List<DatabaseObjectType> _allowedInheritanceParents = [];

    private List<DatabaseObjectType> _allowedNestingParents = [];

    public bool IsInitialized { get; private set; }

    public string Name { get; } = name;

    public string DefaultInstanceName { get; } = defaultInstanceName;

    public string IconName { get; } = iconName;

    public int Order { get; } = order;

    public bool SupportsValue { get; } = supportsValue;

    public bool MustInherit { get; } = mustInherit;

    public int NameLengthLimit { get; } = nameLengthLimit;

    public bool SaveStandalone { get; } = saveStandalone;

    public ReadOnlyList<DatabaseObjectType> AllowedNestingParents => new(_allowedNestingParents);

    public ReadOnlyList<DatabaseObjectType> AllowedInheritanceParents => new(_allowedInheritanceParents);

    public ReadOnlyList<DatabaseObjectType> AllowedNestingChildren => new(_allowedNestingChildren);

    public ReadOnlyList<DatabaseObjectType> AllowedInheritanceChildren => new(_allowedInheritanceChildren);

    public DatabaseObjectType NestedValueType
    {
        get => IsInitialized ? field : throw new InvalidOperationException("NestedValueType was not initialized.");
        private set;
    } = null!;

    public void InitializeRelations(
        IEnumerable<DatabaseObjectType>? allowedNestingParents,
        IEnumerable<DatabaseObjectType>? allowedInheritanceParents,
        DatabaseObjectType nestedValueType
    )
    {
        if (IsInitialized)
        {
            throw new InvalidOperationException("InitializeRelations of this DatabaseObjectType was already called.");
        }

        if (allowedNestingParents is not null)
        {
            _allowedNestingParents = allowedNestingParents.Distinct().ToList();
        }

        var databaseObjectTypes = allowedInheritanceParents?.ToList() ?? [];
        _allowedInheritanceParents = databaseObjectTypes.Distinct().ToList();
        foreach (var allowedNestingParent in _allowedNestingParents)
        {
            allowedNestingParent._allowedNestingChildren.Add(this);
        }

        foreach (var allowedInheritanceParent in databaseObjectTypes)
        {
            allowedInheritanceParent._allowedInheritanceChildren.Add(this);
        }

        NestedValueType = nestedValueType;
        IsInitialized = true;
    }

    public override string ToString()
    {
        return Name;
    }
}
