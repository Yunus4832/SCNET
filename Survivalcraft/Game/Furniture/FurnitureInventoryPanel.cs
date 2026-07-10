using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game;

public class FurnitureInventoryPanel : CanvasWidget
{
    private const string _typeName = nameof(FurnitureInventoryPanel);

    private readonly ButtonWidget _addButton;

    private int _assignedPage;

    private readonly ComponentPlayer _componentPlayer;

    private readonly CreativeInventoryWidget _creativeInventoryWidget;

    private readonly ListPanelWidget _furnitureSetList;

    private bool _ignoreSelectionChanged;

    private readonly GridPanelWidget _inventoryGrid;

    private readonly ButtonWidget _moreButton;

    private int _pagesCount;

    private bool _populateNeeded;

    public FurnitureInventoryPanel(CreativeInventoryWidget creativeInventoryWidget)
    {
        _creativeInventoryWidget = creativeInventoryWidget;
        ComponentFurnitureInventory = creativeInventoryWidget.Entity.FindComponent<ComponentFurnitureInventory>(true)!;
        _componentPlayer = creativeInventoryWidget.Entity.FindComponent<ComponentPlayer>(true)!;
        SubsystemFurnitureBlockBehavior =
            ComponentFurnitureInventory.Project.FindSubsystem<SubsystemFurnitureBlockBehavior>(true)!;
        SubsystemTerrain = ComponentFurnitureInventory.Project.FindSubsystem<SubsystemTerrain>(true)!;
        var node = ContentManager.Get<XElement>("Widgets/FurnitureInventoryPanel");
        LoadContents(this, node);
        _furnitureSetList = Children.Find<ListPanelWidget>("FurnitureSetList")!;
        _inventoryGrid = Children.Find<GridPanelWidget>("InventoryGrid")!;
        _addButton = Children.Find<ButtonWidget>("AddButton")!;
        _moreButton = Children.Find<ButtonWidget>("MoreButton")!;
        for (var i = 0; i < _inventoryGrid.RowsCount; i++)
        for (var j = 0; j < _inventoryGrid.ColumnsCount; j++)
        {
            var widget = new InventorySlotWidget();
            _inventoryGrid.Children.Add(widget);
            _inventoryGrid.SetWidgetCell(widget, new Point2(j, i));
        }

        var furnitureSetList = _furnitureSetList;
        furnitureSetList.ItemWidgetFactory = (Func<object, Widget>)Delegate.Combine(furnitureSetList.ItemWidgetFactory,
            (Func<object, Widget>)(item => new FurnitureSetItemWidget(this, (FurnitureSet)item)));
        _furnitureSetList.SelectionChanged += delegate
        {
            if (_ignoreSelectionChanged || ComponentFurnitureInventory.FurnitureSet ==
                _furnitureSetList.SelectedItem as FurnitureSet)
            {
                return;
            }

            ComponentFurnitureInventory.PageIndex = 0;
            ComponentFurnitureInventory.FurnitureSet =
                _furnitureSetList.SelectedItem as FurnitureSet ?? FurnitureSetDefault.Default;
            if (ComponentFurnitureInventory.FurnitureSet is FurnitureSetDefault)
            {
                _furnitureSetList.SelectedIndex = 0;
            }

            AssignInventorySlots();
        };
        _populateNeeded = true;
    }

    public SubsystemTerrain SubsystemTerrain { get; set; }

    public SubsystemFurnitureBlockBehavior SubsystemFurnitureBlockBehavior { get; set; }

    public ComponentFurnitureInventory ComponentFurnitureInventory { get; set; }

    public override void Update()
    {
        if (_populateNeeded)
        {
            Populate();
            _populateNeeded = false;
        }

        if (ComponentFurnitureInventory.PageIndex != _assignedPage)
        {
            AssignInventorySlots();
        }

        _creativeInventoryWidget.PageUpButton.IsEnabled = ComponentFurnitureInventory.PageIndex > 0;
        _creativeInventoryWidget.PageDownButton.IsEnabled = ComponentFurnitureInventory.PageIndex < _pagesCount - 1;
        _creativeInventoryWidget.PageLabel.Text = _pagesCount > 0
            ? $"{ComponentFurnitureInventory.PageIndex + 1}/{_pagesCount}"
            : string.Empty;
        _moreButton.IsEnabled = ComponentFurnitureInventory.FurnitureSet is not FurnitureSetDefault;
        if (Input.Scroll.HasValue)
        {
            var widget = HitTestGlobal(Input.Scroll.Value.XY);
            if (widget != null && widget.IsChildWidgetOf(_inventoryGrid))
            {
                ComponentFurnitureInventory.PageIndex -= (int)Input.Scroll.Value.Z;
            }
        }

        if (_creativeInventoryWidget.PageUpButton.IsClicked)
        {
            --ComponentFurnitureInventory.PageIndex;
        }

        if (_creativeInventoryWidget.PageDownButton.IsClicked)
        {
            ++ComponentFurnitureInventory.PageIndex;
        }

        ComponentFurnitureInventory.PageIndex = _pagesCount > 0
            ? MathUtils.Clamp(ComponentFurnitureInventory.PageIndex, 0, _pagesCount - 1)
            : 0;
        if (_addButton.IsClicked)
        {
            var list = new List<Tuple<string, Action>>
            {
                new(LanguageManager.Get(_typeName, 6), delegate
                {
                    if (SubsystemFurnitureBlockBehavior.FurnitureSets.Count < 32)
                    {
                        NewFurnitureSet();
                    }
                    else
                    {
                        DialogsManager.ShowDialog(
                            _componentPlayer.GuiWidget,
                            new MessageDialog(
                                LanguageManager.Get(_typeName, 24),
                                LanguageManager.Get(_typeName, 25),
                                LanguageManager.Get("Usual", "ok")
                            )
                        );
                    }
                }),
                new(LanguageManager.Get(_typeName, 7), delegate { ImportFurnitureSet(SubsystemTerrain); })
            };
            DialogsManager.ShowDialog(
                _componentPlayer.GuiWidget,
                new ListSelectionDialog(
                    LanguageManager.Get(_typeName, 8),
                    list,
                    64f,
                    t => ((Tuple<string, Action>)t).Item1,
                    delegate(object t) { ((Tuple<string, Action>)t).Item2(); }
                )
            );
        }

        if (!_moreButton.IsClicked || ComponentFurnitureInventory.FurnitureSet is FurnitureSetDefault)
        {
            return;
        }

        var list2 = new List<Tuple<string, Action>>
        {
            new(LanguageManager.Get(_typeName, 9), RenameFurnitureSet),
            new(LanguageManager.Get(_typeName, 28), delegate
            {
                if (SubsystemFurnitureBlockBehavior
                    .GetFurnitureSetDesigns(ComponentFurnitureInventory.FurnitureSet)
                    .Any()
                   )
                {
                    DialogsManager.ShowDialog(
                        _componentPlayer.GuiWidget,
                        new MessageDialog(
                            LanguageManager.Get("Usual", "warning"),
                            LanguageManager.Get(_typeName, 26),
                            LanguageManager.Get(_typeName, 27),
                            LanguageManager.Get(_typeName, 28),
                            delegate(MessageDialogButton b)
                            {
                                if (b == MessageDialogButton.Button1)
                                {
                                    DeleteFurnitureSet();
                                }
                            }
                        )
                    );
                }
                else
                {
                    DeleteFurnitureSet();
                }
            }),
            new(LanguageManager.Get(_typeName, 11), delegate { MoveFurnitureSet(-1); }),
            new(LanguageManager.Get(_typeName, 12), delegate { MoveFurnitureSet(1); }),
            new(LanguageManager.Get(_typeName, 13), ExportFurnitureSet)
        };
        DialogsManager.ShowDialog(
            _componentPlayer.GuiWidget,
            new ListSelectionDialog(
                LanguageManager.Get(_typeName, 14),
                list2, 64f,
                t => ((Tuple<string, Action>)t).Item1,
                delegate(object t) { ((Tuple<string, Action>)t).Item2(); }
            )
        );
    }

    public override void UpdateCeases()
    {
        base.UpdateCeases();
        ComponentFurnitureInventory.ClearSlots();
        _populateNeeded = true;
    }

    public void Invalidate()
    {
        _populateNeeded = true;
    }

    public void Populate()
    {
        ComponentFurnitureInventory.FillSlots();
        try
        {
            _ignoreSelectionChanged = true;
            _furnitureSetList.ClearItems();
            _furnitureSetList.AddItem(FurnitureSetDefault.Default);
            foreach (var furnitureSet in SubsystemFurnitureBlockBehavior.FurnitureSets)
            {
                _furnitureSetList.AddItem(furnitureSet);
            }
        }
        finally
        {
            _ignoreSelectionChanged = false;
        }

        _furnitureSetList.SelectedItem = ComponentFurnitureInventory.FurnitureSet;
        AssignInventorySlots();
    }

    public void AssignInventorySlots()
    {
        var list = new List<int>();
        for (var i = 0; i < ComponentFurnitureInventory.SlotsCount; i++)
        {
            var slotValue = ComponentFurnitureInventory.GetSlotValue(i);
            var slotCount = ComponentFurnitureInventory.GetSlotCount(i);
            if (slotValue == 0 ||
                slotCount <= 0 ||
                Terrain.ExtractContents(slotValue) != FurnitureBlock.Index)
            {
                continue;
            }

            var designIndex = FurnitureBlock.GetDesignIndex(Terrain.ExtractData(slotValue));
            var design = SubsystemFurnitureBlockBehavior.GetDesign(designIndex);
            if (design != null && design.FurnitureSet == ComponentFurnitureInventory.FurnitureSet)
            {
                list.Add(i);
            }
        }

        var list2 = new List<InventorySlotWidget>(Enumerable.Cast<InventorySlotWidget>(
            from w in _inventoryGrid.Children
            select w as InventorySlotWidget
            into w
            where w != null
            select w));
        var num = ComponentFurnitureInventory.PageIndex * list2.Count;
        foreach (var item in list2)
        {
            if (num < list.Count)
            {
                item.AssignInventorySlot(ComponentFurnitureInventory, list[num]);
            }
            else
            {
                item.AssignInventorySlot(null, 0);
            }

            num++;
        }

        _pagesCount = (list.Count + list2.Count - 1) / list2.Count;
        _assignedPage = ComponentFurnitureInventory.PageIndex;
    }

    public void NewFurnitureSetLogic(string s, string from = "")
    {
        var furnitureSet = SubsystemFurnitureBlockBehavior.NewFurnitureSet(s, from);
        ComponentFurnitureInventory.FurnitureSet = furnitureSet;
        Populate();
        _furnitureSetList.ScrollToItem(furnitureSet);
    }

    public void NewFurnitureSet()
    {
        _componentPlayer.GuiWidget.Input.EnterText(
            _componentPlayer.GuiWidget,
            LanguageManager.Get(_typeName, 15),
            LanguageManager.Get(_typeName, 16),
            20,
            delegate(string s)
            {
                NewFurnitureSetLogic(s);
                CommonLib.Net.QueuePackage(new FurniturePackage(s));
            }
        );
    }

    public void DeleteFurnitureSetLogic(FurnitureSet furnitureSet)
    {
        if (furnitureSet is FurnitureSetDefault)
        {
            return;
        }

        var num = SubsystemFurnitureBlockBehavior.FurnitureSets.IndexOf(furnitureSet);
        SubsystemFurnitureBlockBehavior.DeleteFurnitureSet(furnitureSet);
        SubsystemFurnitureBlockBehavior.GarbageCollectDesigns();
        ComponentFurnitureInventory.FurnitureSet =
            num > 0 ? SubsystemFurnitureBlockBehavior.FurnitureSets[num - 1] : FurnitureSetDefault.Default;
        Invalidate();
    }

    public void DeleteFurnitureSet()
    {
        var furnitureSet = _furnitureSetList.SelectedItem as FurnitureSet;
        if (furnitureSet is null or FurnitureSetDefault)
        {
            return;
        }

        DeleteFurnitureSetLogic(furnitureSet);
        CommonLib.Net.QueuePackage(new FurniturePackage(furnitureSet.Name));
    }

    public void RenameFurnitureSetLogic(FurnitureSet furnitureSet, string s)
    {
        if (furnitureSet is FurnitureSetDefault)
        {
            return;
        }

        furnitureSet.Name = s;
        Invalidate();
    }

    public void RenameFurnitureSet()
    {
        var furnitureSet = _furnitureSetList.SelectedItem as FurnitureSet;
        if (furnitureSet is null or FurnitureSetDefault)
        {
            return;
        }

        _componentPlayer.GuiWidget.Input.EnterText(
            _componentPlayer.GuiWidget,
            LanguageManager.Get(_typeName, 17),
            furnitureSet.Name,
            20,
            delegate(string s)
            {
                RenameFurnitureSetLogic(furnitureSet, s);
                CommonLib.Net.QueuePackage(new FurniturePackage(furnitureSet.Name, s));
            }
        );
    }

    public void MoveFurnitureSetLogic(FurnitureSet furnitureSet, int move)
    {
        if (furnitureSet is FurnitureSetDefault)
        {
            return;
        }

        SubsystemFurnitureBlockBehavior.MoveFurnitureSet(furnitureSet, move);
        Invalidate();
    }

    public void MoveFurnitureSet(int move)
    {
        if (_furnitureSetList.SelectedItem is not FurnitureSet furnitureSet)
        {
            return;
        }

        MoveFurnitureSetLogic(furnitureSet, move);
        CommonLib.Net.QueuePackage(new FurniturePackage(furnitureSet, move));
    }

    private void ImportFurnitureSet(SubsystemTerrain subsystemTerrain, string text)
    {
        var num = 0;
        var num2 = 0;
        var list =
            FurnitureDesign.ListChains(FurniturePacksManager.LoadFurniturePack(subsystemTerrain, text));
        var list2 = new List<FurnitureDesign>();
        SubsystemFurnitureBlockBehavior.GarbageCollectDesigns();
        foreach (var item in list)
        {
            var furnitureDesign = SubsystemFurnitureBlockBehavior.TryAddDesignChain(item[0], false);
            if (furnitureDesign == item[0])
            {
                list2.Add(furnitureDesign);
            }
            else if (furnitureDesign == null)
            {
                num2++;
            }
            else
            {
                num++;
            }
        }

        if (list2.Count > 0)
        {
            var furnitureSet =
                SubsystemFurnitureBlockBehavior.NewFurnitureSet(FurniturePacksManager.GetDisplayName(text), text);
            if (CommonLib.WorkType == WorkType.Server)
            {
                CommonLib.Net.QueuePackage(new FurniturePackage(furnitureSet));
            }

            foreach (var item2 in list2)
            {
                SubsystemFurnitureBlockBehavior.AddToFurnitureSet(item2, furnitureSet);
                if (CommonLib.WorkType == WorkType.Server)
                {
                    CommonLib.Net.QueuePackage(new FurniturePackage(item2, furnitureSet));
                }
            }

            ComponentFurnitureInventory.FurnitureSet = furnitureSet;
        }

        Invalidate();
        var text2 = string.Format(LanguageManager.Get(_typeName, 1), list2.Count);
        if (num > 0)
        {
            text2 += string.Format(LanguageManager.Get(_typeName, 2), num);
        }

        if (num2 > 0)
        {
            text2 += string.Format(LanguageManager.Get(_typeName, 3), num2, 65535);
        }

        DialogsManager.ShowDialog(
            _componentPlayer.GuiWidget,
            new MessageDialog(
                LanguageManager.Get(_typeName, 4),
                text2.Trim(),
                LanguageManager.Get("Usual", "ok")
            )
        );
    }

    public void ImportFurnitureSet(SubsystemTerrain subsystemTerrain)
    {
        FurniturePacksManager.UpdateFurniturePacksList();
        if (FurniturePacksManager.ReadOnlyFurniturePackNames.Count == 0)
        {
            DialogsManager.ShowDialog(
                _componentPlayer.GuiWidget,
                new MessageDialog(
                    LanguageManager.Get(_typeName, 18),
                    LanguageManager.Get(_typeName, 19),
                    LanguageManager.Ok
                )
            );
        }
        else
        {
            DialogsManager.ShowDialog(_componentPlayer.GuiWidget, new ListSelectionDialog(
                LanguageManager.Get(_typeName, 20), FurniturePacksManager.ReadOnlyFurniturePackNames, 64f,
                s => FurniturePacksManager.GetDisplayName((string)s), delegate(object s)
                {
                    try
                    {
                        var text = (string)s;
                        ImportFurnitureSet(subsystemTerrain, text);
                    }
                    catch (Exception ex)
                    {
                        DialogsManager.ShowDialog(
                            _componentPlayer.GuiWidget,
                            new MessageDialog(
                                LanguageManager.Get(_typeName, 5),
                                ex.Message,
                                LanguageManager.Ok
                            )
                        );
                    }
                }));
        }
    }

    public void ExportFurnitureSet()
    {
        try
        {
            var designs =
                SubsystemFurnitureBlockBehavior.GetFurnitureSetDesigns(ComponentFurnitureInventory.FurnitureSet)
                    .ToArray();
            var displayName = FurniturePacksManager.GetDisplayName(
                FurniturePacksManager.CreateFurniturePack(ComponentFurnitureInventory.FurnitureSet.Name, designs));
            DialogsManager.ShowDialog(
                _componentPlayer.GuiWidget,
                new MessageDialog(
                    LanguageManager.Get(_typeName, 21),
                    string.Format(LanguageManager.Get(_typeName, 22), displayName),
                    LanguageManager.Get("Usual", "ok")
                )
            );
        }
        catch (Exception ex)
        {
            DialogsManager.ShowDialog(
                _componentPlayer.GuiWidget,
                new MessageDialog(
                    LanguageManager.Get(_typeName, 23),
                    ex.Message,
                    LanguageManager.Get("Usual", "ok")
                )
            );
        }
    }
}
