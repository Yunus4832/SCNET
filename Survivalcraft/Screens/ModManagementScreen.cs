using System.Xml.Linq;

using Content.Packaging;

using Game.Content;
using Game.Modding;

namespace Game.Screens;

public sealed class ModManagementScreen : Screen
{
    private const string _typeName = nameof(ModManagementScreen);

    private enum ModAction
    {
        Import,
        Export,
        Global,
        World,
        DeleteCache,
        Refresh
    }

    private readonly ActionPanelWidget _actionPanel;
    private readonly LabelWidget _emptyLabel;
    private readonly ListPanelWidget _modsList;
    private readonly LabelWidget _pickerUnavailableLabel;
    private bool _busy;
    private ModProfile _globalProfile = new();
    private int _operationGeneration;
    private BusyDialog? _operationDialog;

    public ModManagementScreen()
    {
        LoadContents(this, ContentManager.Get<XElement>("Screens/ModManagementScreen"));
        _actionPanel = Children.Find<ActionPanelWidget>("Actions")!;
        _emptyLabel = Children.Find<LabelWidget>("Empty")!;
        _modsList = Children.Find<ListPanelWidget>("ModsList")!;
        _pickerUnavailableLabel = Children.Find<LabelWidget>("PickerUnavailable")!;
        _actionPanel.ItemTextProvider = GetActionText;
        _actionPanel.ItemEnabledProvider = IsActionEnabled;
        _actionPanel.ItemColorProvider = item => item is ModAction.DeleteCache ? new Color(150, 50, 35) : null;
        _actionPanel.ItemClicked += ExecuteAction;
        _actionPanel.SetPrimaryItems(
        [
            ModAction.Import,
            ModAction.Export,
            ModAction.Global,
            ModAction.World
        ]);
        _actionPanel.SetSecondaryItems(
        [
            ModAction.Refresh,
            ModAction.DeleteCache
        ]);
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
        _busy = false;
        if (_operationDialog is not null)
        {
            DialogsManager.HideDialog(_operationDialog);
            _operationDialog = null;
        }

        _actionPanel.ShowPrimaryItems();
        _modsList.SelectedItem = null;
    }

    public override void Update()
    {
        var pickerAvailable = FilePicker.IsAvailable;
        _pickerUnavailableLabel.IsVisible = !pickerAvailable;
        _actionPanel.IsEnabled = !_busy;
        _actionPanel.Refresh();

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }
    }

    private bool IsActionEnabled(object item)
    {
        if (_busy || item is not ModAction action)
        {
            return false;
        }

        var selected = _modsList.SelectedItem as ManagedModItem;
        return action switch
        {
            ModAction.Import => FilePicker.IsAvailable,
            ModAction.Export => FilePicker.IsAvailable && selected?.LocalEntry is not null,
            ModAction.Global or ModAction.World => selected is not null,
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

        var selected = _modsList.SelectedItem as ManagedModItem;
        switch (action)
        {
            case ModAction.Import:
                ImportPackages();
                break;
            case ModAction.Export when selected is not null:
                ExportPackage(selected);
                break;
            case ModAction.Global when selected is not null:
                ToggleGlobal(selected);
                break;
            case ModAction.World when selected is not null:
                SelectWorldsForPackage(selected);
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

        var selected = _modsList.SelectedItem as ManagedModItem;
        return action switch
        {
            ModAction.Global => LanguageManager.Get(_typeName,
                selected?.IsGlobal == true ? "RemoveGlobal" : "AddGlobal"),
            ModAction.DeleteCache => LanguageManager.Get(_typeName, "DeleteCacheShort"),
            ModAction.Refresh => LanguageManager.Get(_typeName, "RefreshLocal"),
            _ => LanguageManager.Get(_typeName, action.ToString())
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
        WorldsManager.UpdateWorldsList();
        _globalProfile = ModProfileManager.LoadGlobalProfile();
        var worldProfiles = WorldsManager.WorldInfos
            .Select(world => ModProfileManager.LoadWorldProfile(world.DirectoryName))
            .OfType<ModProfile>()
            .ToArray();
        var repository = CreateRepository();
        var selected = _modsList.SelectedItem as ManagedModItem;
        var items = ModManagementCatalog.Build(repository.ListAll(), _globalProfile, worldProfiles,
            CurrentModRuntime.Value?.EffectiveProfile);
        _modsList.ClearItems();
        foreach (var item in items)
        {
            _modsList.AddItem(item);
            if (selected is not null && IsSamePackage(selected, item))
            {
                _modsList.SelectedItem = item;
            }
        }

        _emptyLabel.IsVisible = items.Count == 0;
    }

    private void ConfirmDeleteCache(ManagedModItem item)
    {
        var message = string.Format(LanguageManager.Get(_typeName, "DeleteCacheQuestion"),
            item.ModId, item.Version);
        DialogsManager.ShowDialog(null, new MessageDialog(
            LanguageManager.Get(_typeName, "DeleteCacheTitle"),
            message,
            LanguageManager.Yes,
            LanguageManager.No,
            button =>
            {
                if (button == MessageDialogButton.Button1)
                {
                    DeleteCache(item);
                }
            }));
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
            DialogsManager.Alert(LanguageManager.Get(_typeName, "ReferencedCannotDelete"));
        }
        catch (Exception exception)
        {
            Log.Error($"Mod cache removal failed: {exception}");
            DialogsManager.Alert(LanguageManager.Get(_typeName, "DeleteCacheFailed"));
        }
    }

    private async void ImportPackages()
    {
        var generation = _operationGeneration;
        _busy = true;
        try
        {
            var files = await FilePicker.PickFilesAsync(new FilePickerRequest([ContentPackageReader.FileExtension],
                AllowMultiple: true, Title: LanguageManager.Get(_typeName, "SelectPackages")));
            if (files.Count == 0)
            {
                return;
            }

            if (!IsCurrentOperation(generation))
            {
                return;
            }

            var busyDialog = new BusyDialog(LanguageManager.Get(_typeName, "Importing"), string.Empty);
            _operationDialog = busyDialog;
            Dispatcher.Dispatch(() =>
            {
                if (IsCurrentOperation(generation))
                {
                    DialogsManager.ShowDialog(null, busyDialog);
                }
            });
            var cache = new ContentPackageCache(Storage.GetSystemPath(GamePaths.ContentPackageCache));
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
                });
            }

            Dispatcher.Dispatch(() =>
            {
                if (!IsCurrentOperation(generation))
                {
                    return;
                }

                RefreshState();
                DialogsManager.Alert(string.Format(LanguageManager.Get(_typeName, "ImportComplete"), imported));
            });
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() =>
            {
                if (IsCurrentOperation(generation))
                {
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "ImportModFailed"), exception.Message);
                }
            });
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
            var target = await FilePicker.PickSaveTargetAsync(new FileSaveRequest(
                $"{item.ModId}-{item.Version}{ContentPackageReader.FileExtension}",
                "application/vnd.scnet.content-package", LanguageManager.Get(_typeName, "ExportTitle")));
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
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportComplete"), target.Name);
                }
            });
        }
        catch (Exception exception)
        {
            Dispatcher.Dispatch(() =>
            {
                if (IsCurrentOperation(generation))
                {
                    DialogsManager.Alert(LanguageManager.Get(_typeName, "ExportModFailed"), exception.Message);
                }
            });
        }
        finally
        {
            if (generation == _operationGeneration)
            {
                _busy = false;
            }
        }
    }

    private void SelectWorldsForPackage(ManagedModItem item)
    {
        WorldsManager.UpdateWorldsList();
        if (WorldsManager.WorldInfos.Count == 0)
        {
            DialogsManager.Alert(
                LanguageManager.Get(_typeName, "NoWorldTitle"),
                LanguageManager.Get(_typeName, "NoWorldMessage"));
            return;
        }

        DialogsManager.ShowDialog(null, new ModWorldSelectionDialog(
            $"{item.ModId}@{item.Version}",
            WorldsManager.WorldInfos,
            world => ModProfileManager.LoadWorldProfile(world.DirectoryName) is { } profile &&
                     ModManagementCatalog.ContainsExact(profile, item),
            selections =>
            {
                foreach (var selection in selections)
                {
                    var profile = ModProfileManager.LoadWorldProfile(selection.World.DirectoryName) ??
                                  new ModProfile();
                    if (selection.IsChecked)
                    {
                        AddPackage(profile, item);
                    }
                    else if (ModManagementCatalog.ContainsExact(profile, item))
                    {
                        RemovePackage(profile, item.ModId);
                    }

                    ModProfileManager.SaveWorldProfile(selection.World.DirectoryName, profile);
                }

                RefreshState();
            }));
    }

    private static void AddPackage(ModProfile profile, ManagedModItem item)
    {
        RemovePackage(profile, item.ModId);
        profile.Packages.Add(item.ToRequirement());
    }

    private static void RemovePackage(ModProfile profile, string modId)
    {
        profile.Packages.RemoveAll(package =>
            string.Equals(package.ModId, modId, StringComparison.OrdinalIgnoreCase));
    }

    private static LocalModRepository CreateRepository()
    {
        return new LocalModRepository(Storage.GetSystemPath(GamePaths.ContentPackageCache));
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

    private static Widget CreateModItemWidget(object item)
    {
        var mod = (ManagedModItem)item;
        var status = new List<string>();
        status.Add(LanguageManager.Get(_typeName, mod.IsMissing ? "StatusMissing" : "StatusCached"));
        if (mod.IsGlobal)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusGlobal"));
        }

        if (mod.IsWorld)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusWorld"));
        }

        if (mod.IsRuntime)
        {
            status.Add(LanguageManager.Get(_typeName, "StatusRuntime"));
        }

        return new StackPanelWidget
        {
            Direction = LayoutDirection.Vertical,
            Children =
            {
                new LabelWidget
                {
                    Text = $"{mod.ModId}  {mod.Version}",
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Center
                },
                new LabelWidget
                {
                    Text = $"{mod.PackageHash} | {string.Join(" / ", status)}",
                    Color = Color.Gray,
                    FontScale = 0.55f,
                    HorizontalAlignment = WidgetAlignment.Near,
                    VerticalAlignment = WidgetAlignment.Center
                }
            }
        };
    }
}
