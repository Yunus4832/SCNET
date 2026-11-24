using System.Xml.Linq;

using Content.Packaging;

using Game.Application;
using Game.Content;

namespace Game.Screens;

public sealed class ModManagementScreen : Screen
{
    private const string _typeName = nameof(ModManagementScreen);

    private enum ModAction
    {
        Import = 0,

        Export = 1,

        Details = 2,

        Global = 3,

        DeleteCache = 4,

        Refresh = 5
    }

    private readonly ActionPanelWidget _actionPanel;

    private readonly LabelWidget _emptyLabel;

    private readonly ListPanelWidget _modsList;

    private readonly LabelWidget _pickerUnavailableLabel;

    private readonly SelectionDrawerWidget _versions;

    private ManagedModItem? SelectedPackage => _versions.SelectedItem as ManagedModItem;

    private bool _busy;

    private ModProfile _globalProfile = new();

    private int _operationGeneration;

    private BusyDialog? _operationDialog;

    public ModManagementScreen()
    {
        LoadContents(this, AssetManager.Get<XElement>("Screens/ModManagementScreen"));
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _emptyLabel = Children.Find<LabelWidget>("Empty")!;
        _modsList = Children.Find<ListPanelWidget>("ModsList")!;
        _pickerUnavailableLabel = Children.Find<LabelWidget>("PickerUnavailable")!;
        _versions = new SelectionDrawerWidget
        {
            Size = new Vector2(140f, 60f),
            ExpansionDirection = SelectionDrawerDirection.Up,
            MaxVisibleItems = 5,
            PlaceholderText =
                Localization.ClientLanguage.Catalog.GetContentWidgets(nameof(OnlineContentScreen), "Version"),
            ItemTextProvider = item => FormatVersion(
                (ManagedModItem)item,
                _modsList.SelectedItem as ManagedModItem[] ?? []
            )
        };
        _versions.SelectionChanged += _actionPanel.Refresh;
        _modsList.SelectionChanged += PopulateVersions;
        _actionPanel.PrimaryAccessory = _versions;
        _actionPanel.ItemTextProvider = GetActionText;
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemColorProvider = item => item is ModAction.DeleteCache ? new Color(150, 50, 35) : null;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
            [
                ModAction.Import,
                ModAction.Export,
                ModAction.Global
            ],
            [1.8f, 0.8f, 0.8f, 1.1f]
        );
        _actionPanel.SetSecondaryItems(
            [
                ModAction.Details,
                ModAction.Refresh,
                ModAction.DeleteCache
            ]
        );
        _modsList.ItemWidgetFactory = CreateModItemWidget;
    }

    public override void Enter(object[] parameters)
    {
        _operationGeneration++;
        RefreshState();
    }

    public override void Leave()
    {
        _operationGeneration++;
        _versions.Close();
        _busy = false;
        if (_operationDialog is not null)
        {
            DialogsManager.HideDialog(_operationDialog);
            _operationDialog = null;
        }

        _actionPanel.ShowPrimaryItems();
        _modsList.SelectedItem = null;
    }

    public override void Dispose()
    {
        _versions.Close();
        base.Dispose();
    }

    public override void Update()
    {
        var pickerAvailable = FilePicker.IsAvailable;
        _pickerUnavailableLabel.IsVisible = !pickerAvailable;
        _actionPanel.IsEnabled = !_busy;
        _actionPanel.Refresh();

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            if (_versions.IsOpen) { _versions.Close(); }
            else { ScreensManager.SwitchScreen("Content"); }
        }
    }

    private bool IsActionEnabled(object item)
    {
        if (_busy || item is not ModAction action)
        {
            return false;
        }

        var selected = SelectedPackage;
        return action switch
        {
            ModAction.Import => FilePicker.IsAvailable,
            ModAction.Export => FilePicker.IsAvailable && selected?.LocalEntry is not null,
            ModAction.Global => selected is not null,
            ModAction.Details => selected?.LocalEntry is not null,
            ModAction.DeleteCache => selected?.LocalEntry is not null,
            ModAction.Refresh => true,
            _ => false
        };
    }

    private void ExecuteAction(object item)
    {
        if (item is not ModAction action || !IsActionEnabled(action))
        {
            return;
        }

        var selected = SelectedPackage;
        switch (action)
        {
            case ModAction.Import:
                ImportPackages();
                break;
            case ModAction.Export when selected is not null:
                ExportPackage(selected);
                break;
            case ModAction.Details when selected?.LocalEntry is { } local:
                DialogsManager.Alert(
                    $"{local.Name}\n{selected.ModId}@{selected.Version}\n{selected.PackageHash}\n\n{local.Description}"
                );
                break;
            case ModAction.Global when selected is not null:
                ToggleGlobal(selected);
                break;
            case ModAction.DeleteCache when selected is not null:
                ConfirmDeleteCache(selected);
                break;
            case ModAction.Refresh:
                RefreshState();
                break;
        }
    }

    private string GetActionText(object item)
    {
        if (item is not ModAction action)
        {
            return string.Empty;
        }

        var selected = SelectedPackage;
        return action switch
        {
            ModAction.Global => Localization.ClientLanguage.Catalog.Get(
                _typeName,
                selected?.IsGlobal == true
                    ? "RemoveGlobal"
                    : selected is not null &&
                      _globalProfile.Packages.Any(package =>
                          string.Equals(package.ModId, selected.ModId, StringComparison.OrdinalIgnoreCase)
                      )
                        ? "ReplaceGlobal"
                        : "AddGlobal"
            ),
            ModAction.DeleteCache => Localization.ClientLanguage.Catalog.Get(_typeName, "DeleteCacheShort"),
            ModAction.Refresh => Localization.ClientLanguage.Catalog.Get(_typeName, "RefreshLocal"),
            _ => Localization.ClientLanguage.Catalog.Get(_typeName, action.ToString())
        };
    }

    private void ToggleGlobal(ManagedModItem item)
    {
        if (item.IsGlobal)
        {
            RemovePackage(_globalProfile, item.ModId);
        }
        else
        {
            AddPackage(_globalProfile, item);
        }

        ModProfileManager.SaveGlobalProfile(_globalProfile);
        RefreshState();
    }

    private void RefreshState()
    {
        ClientApplication.Worlds.UpdateWorldsList();
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        var worldProfiles = ClientApplication.Worlds.WorldInfos
            .Select(world => ModProfileManager.LoadWorldProfile(world.DirectoryName))
            .OfType<ModProfile>()
            .ToArray();
        var repository = CreateRepository();
        var selected = SelectedPackage;
        var items = ModManagementCatalog.Build(
            repository.ListAll(),
            _globalProfile,
            worldProfiles,
            CurrentModRuntime.Value?.LoadedProfile
        );
        PopulateGroups(items, selected);
    }

    private void PopulateGroups(IReadOnlyList<ManagedModItem> items, ManagedModItem? selected)
    {
        var scroll = _modsList.ScrollPosition;
        _modsList.ClearItems();
        foreach (var packages in items.GroupBy(item => item.ModId, StringComparer.OrdinalIgnoreCase))
        {
            var group = packages.ToArray();
            _modsList.AddItem(group);
            if (string.Equals(packages.Key, selected?.ModId, StringComparison.OrdinalIgnoreCase))
            {
                _modsList.SelectedItem = group;
                if (group.FirstOrDefault(item => IsSamePackage(item, selected!)) is { } restored)
                {
                    _versions.SelectedItem = restored;
                }
            }
        }

        _modsList.ScrollPosition = scroll;
        _emptyLabel.IsVisible = items.Count == 0;
        if (_modsList.SelectedItem is null) { PopulateVersions(); }
    }

    private void PopulateVersions()
    {
        _versions.Close();
        var packages = _modsList.SelectedItem as ManagedModItem[] ?? [];
        _versions.SetItems(packages);
        _versions.SelectedItem = packages.FirstOrDefault(item => item.IsGlobal) ??
                                 packages.FirstOrDefault(item => !item.IsMissing) ?? packages.FirstOrDefault();
        _versions.IsEnabled = packages.Length > 1;
        _actionPanel.Refresh();
    }

    private void ConfirmDeleteCache(ManagedModItem item)
    {
        var message = string.Format(
            Localization.ClientLanguage.Catalog.Get(_typeName, "DeleteCacheQuestion"),
            item.ModId,
            $"{item.Version} · {item.PackageHash[..8]}"
        );
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, "DeleteCacheTitle"),
                message,
                Localization.ClientLanguage.Catalog.Yes,
                Localization.ClientLanguage.Catalog.No,
                button =>
                {
                    if (button == MessageDialogButton.Button1)
                    {
                        DeleteCache(item);
                    }
                }
            )
        );
    }

    private void DeleteCache(ManagedModItem item)
    {
        if (item.LocalEntry is null)
        {
            return;
        }

        try
        {
            CreateRepository().DeletePackage(item.LocalEntry);
            RefreshState();
        }
        catch (InvalidOperationException exception)
        {
            Log.Warning($"Referenced mod cache removal was rejected: {exception.Message}");
            DialogsManager.Alert(Localization.ClientLanguage.Catalog.Get(_typeName, "ReferencedCannotDelete"));
        }
        catch (Exception exception)
        {
            Log.Error($"Mod cache removal failed: {exception}");
            DialogsManager.Alert(Localization.ClientLanguage.Catalog.Get(_typeName, "DeleteCacheFailed"));
        }
    }

    private async void ImportPackages()
    {
        var generation = _operationGeneration;
        _busy = true;
        try
        {
            var files = await FilePicker.PickFilesAsync(
                new FilePickerRequest(
                    [ContentPackageReader.FileExtension],
                    AllowMultiple: true,
                    Title: Localization.ClientLanguage.Catalog.Get(_typeName, "SelectPackages")
                )
            );
            if (files.Count == 0)
            {
                return;
            }

            if (!IsCurrentOperation(generation))
            {
                return;
            }

            var busyDialog = new BusyDialog(
                Localization.ClientLanguage.Catalog.Get(_typeName, "Importing"),
                string.Empty
            );
            _operationDialog = busyDialog;
            Dispatcher.Dispatch(() =>
                {
                    if (IsCurrentOperation(generation))
                    {
                        DialogsManager.ShowDialog(null, busyDialog);
                    }
                }
            );
            var cache = new ContentPackageCache(Storage.GetSystemPath(ClientApplication.Paths.ContentPackageCache));
            var imported = 0;
            try
            {
                foreach (var file in files)
                {
                    await using var source = await file.OpenReadAsync(CancellationToken.None);
                    await cache.ImportExpectedAsync(source, ContentPackageType.Mod);
                    imported++;
                }
            }
            finally
            {
                Dispatcher.Dispatch(() =>
                    {
                        if (ReferenceEquals(_operationDialog, busyDialog))
                        {
                            DialogsManager.HideDialog(busyDialog);
                            _operationDialog = null;
                        }
                    }
                );
            }

            Dispatcher.Dispatch(() =>
                {
                    if (!IsCurrentOperation(generation))
                    {
                        return;
                    }

                    RefreshState();
                    DialogsManager.Alert(
                        string.Format(
                            Localization.ClientLanguage.Catalog.Get(_typeName, "ImportComplete"),
                            imported
                        )
                    );
                }
            );
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() =>
                {
                    if (IsCurrentOperation(generation))
                    {
                        DialogsManager.Alert(
                            Localization.ClientLanguage.Catalog.Get(_typeName, "ImportModFailed"),
                            exception.Message
                        );
                    }
                }
            );
        }
        finally
        {
            if (generation == _operationGeneration)
            {
                _busy = false;
            }
        }
    }

    private async void ExportPackage(ManagedModItem item)
    {
        if (item.LocalEntry is null)
        {
            return;
        }

        var generation = _operationGeneration;
        _busy = true;
        try
        {
            var target = await FilePicker.PickSaveTargetAsync(
                new FileSaveRequest(
                    $"{item.ModId}-{item.Version}{ContentPackageReader.FileExtension}",
                    "application/vnd.scnet.content-package",
                    Localization.ClientLanguage.Catalog.Get(_typeName, "ExportTitle")
                )
            );
            if (target is null)
            {
                return;
            }

            if (!IsCurrentOperation(generation))
            {
                return;
            }

            await using var destination = await target.OpenWriteAsync(CancellationToken.None);
            CreateRepository().ExportPackage(item.LocalEntry, destination);
            Dispatcher.Dispatch(() =>
                {
                    if (IsCurrentOperation(generation))
                    {
                        DialogsManager.Alert(
                            Localization.ClientLanguage.Catalog.Get(_typeName, "ExportComplete"),
                            target.Name
                        );
                    }
                }
            );
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() =>
                {
                    if (IsCurrentOperation(generation))
                    {
                        DialogsManager.Alert(
                            Localization.ClientLanguage.Catalog.Get(_typeName, "ExportModFailed"),
                            exception.Message
                        );
                    }
                }
            );
        }
        finally
        {
            if (generation == _operationGeneration)
            {
                _busy = false;
            }
        }
    }

    private static void AddPackage(ModProfile profile, ManagedModItem item)
    {
        RemovePackage(profile, item.ModId);
        profile.Packages.Add(item.ToRequirement());
    }

    private static void RemovePackage(ModProfile profile, string modId)
    {
        profile.Packages.RemoveAll(package =>
            string.Equals(package.ModId, modId, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static LocalModRepository CreateRepository()
    {
        return new LocalModRepository(Storage.GetSystemPath(ClientApplication.Paths.ContentPackageCache));
    }

    private bool IsCurrentOperation(int generation)
    {
        return generation == _operationGeneration && ReferenceEquals(ScreensManager.CurrentScreen, this);
    }

    private static bool IsSamePackage(ManagedModItem first, ManagedModItem second)
    {
        return string.Equals(first.ModId, second.ModId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.Version, second.Version, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.PackageHash, second.PackageHash, StringComparison.Ordinal);
    }

    private static string FormatVersion(ManagedModItem package, ManagedModItem[] group) =>
        group.Count(other => other.Version == package.Version) > 1
            ? $"{package.Version} · {package.PackageHash[..8]}"
            : package.Version;

    private static Widget CreateModItemWidget(object item)
    {
        var group = (ManagedModItem[])item;
        var global = group.FirstOrDefault(package => package.IsGlobal);
        var mod = global?.LocalEntry is not null
            ? global
            : group.FirstOrDefault(package => !package.IsMissing) ?? group[0];
        var widget = (ContainerWidget)LoadWidget(null, AssetManager.Get<XElement>("Widgets/ModListItem"), null);
        widget.Children.Find<LabelWidget>("Name")!.Text = mod.LocalEntry?.Name ?? mod.ModId;
        var globalStatus = global is null
            ? Localization.ClientLanguage.Catalog.Get(_typeName, "GlobalOff")
            : $"{Localization.ClientLanguage.Catalog.Get(_typeName, "StatusGlobal")} · {FormatVersion(global, group)}";
        var count = string.Format(Localization.ClientLanguage.Catalog.Get(_typeName, "PackageCount"), group.Length);
        var details = widget.Children.Find<LabelWidget>("Details")!;
        details.Text = $"{globalStatus}  |  {count}";
        if ((global ?? mod).IsMissing)
        {
            details.Text += $"  |  {Localization.ClientLanguage.Catalog.Get(_typeName, "StatusMissing")}";
            details.Color = new Color(255, 180, 80);
        }

        if (mod.LocalEntry?.Name is { Length: > 0 } name && name != mod.ModId)
        {
            details.Text += $"  |  {mod.ModId}";
        }

        return widget;
    }
}
