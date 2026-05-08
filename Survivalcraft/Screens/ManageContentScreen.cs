using System.Xml.Linq;

namespace Game.Screens;

public class ManageContentScreen : Screen
{
    private const string _typeName = "ManageContentScreen";

    private readonly BlocksTexturesCache _blocksTexturesCache = new();

    private readonly ButtonWidget _changeFilterButton;

    private readonly CharacterSkinsCache _characterSkinsCache = new();

    private readonly ListPanelWidget _contentList;

    private readonly ButtonWidget _deleteButton;

    private ExternalContentType _filter;

    private readonly LabelWidget _filterLabel;

    private readonly ButtonWidget _uploadButton;

    public ManageContentScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ManageContentScreen");
        LoadContents(this, node);
        _contentList = Children.Find<ListPanelWidget>("ContentList")!;
        _deleteButton = Children.Find<ButtonWidget>("DeleteButton")!;
        _uploadButton = Children.Find<ButtonWidget>("UploadButton")!;
        _changeFilterButton = Children.Find<ButtonWidget>("ChangeFilter")!;
        _filterLabel = Children.Find<LabelWidget>("Filter")!;
        _contentList.ItemWidgetFactory = delegate(object obj)
        {
            var listItem = (ListItem)obj;
            ContainerWidget containerWidget;
            if (listItem.Type == ExternalContentType.BlocksTexture)
            {
                var node2 = ContentManager.Get<XElement>("Widgets/BlocksTextureItem");
                containerWidget = (ContainerWidget)LoadWidget(this, node2, null);
                var rectangleWidget = containerWidget.Children.Find<RectangleWidget>("BlocksTextureItem.Icon")!;
                var labelWidget = containerWidget.Children.Find<LabelWidget>("BlocksTextureItem.Text")!;
                var labelWidget2 = containerWidget.Children.Find<LabelWidget>("BlocksTextureItem.Details")!;
                var texture = _blocksTexturesCache.GetTexture(listItem.Name);
                BlocksTexturesManager.GetCreationDate(listItem.Name);
                rectangleWidget.Subtexture = new Subtexture(texture, Vector2.Zero, Vector2.One);
                labelWidget.Text = listItem.DisplayName;
                labelWidget2.Text = string.Format(LanguageControl.Get(_typeName, 1), texture.Width, texture.Height);
                if (listItem.IsBuiltIn)
                {
                    return containerWidget;
                }

                labelWidget2.Text += $" | {listItem.CreationTime.ToLocalTime():dd MMM yyyy HH:mm}";
                if (listItem.UseCount > 0)
                {
                    labelWidget2.Text += string.Format(LanguageControl.Get(_typeName, 2), listItem.UseCount);
                }
            }
            else
            {
                if (listItem.Type != ExternalContentType.CharacterSkin)
                {
                    if (listItem.Type != ExternalContentType.FurniturePack)
                    {
                        throw new InvalidOperationException(LanguageControl.Get(_typeName, 10));
                    }

                    var node3 = ContentManager.Get<XElement>("Widgets/FurniturePackItem");
                    containerWidget = (ContainerWidget)LoadWidget(this, node3, null);
                    var labelWidget3 = containerWidget.Children.Find<LabelWidget>("FurniturePackItem.Text")!;
                    var labelWidget4 = containerWidget.Children.Find<LabelWidget>("FurniturePackItem.Details")!;
                    labelWidget3.Text = listItem.DisplayName;
                    try
                    {
                        var designs = FurniturePacksManager.LoadFurniturePack(null, listItem.Name);
                        labelWidget4.Text = string.Format(LanguageControl.Get(_typeName, 3),
                            FurnitureDesign.ListChains(designs).Count);
                        if (string.IsNullOrEmpty(listItem.Name))
                        {
                            return containerWidget;
                        }

                        labelWidget4.Text += $" | {listItem.CreationTime.ToLocalTime():dd MMM yyyy HH:mm}";
                        return containerWidget;
                    }
                    catch (Exception ex)
                    {
                        labelWidget4.Text = labelWidget4.Text + LanguageControl.Get("Usual", "error") + ex.Message;
                        return containerWidget;
                    }
                }

                var node4 = ContentManager.Get<XElement>("Widgets/CharacterSkinItem");
                containerWidget = (ContainerWidget)LoadWidget(this, node4, null);
                var playerModelWidget = containerWidget.Children.Find<PlayerModelWidget>("CharacterSkinItem.Model")!;
                var labelWidget5 = containerWidget.Children.Find<LabelWidget>("CharacterSkinItem.Text")!;
                var labelWidget6 = containerWidget.Children.Find<LabelWidget>("CharacterSkinItem.Details")!;
                var texture2 = _characterSkinsCache.GetTexture(listItem.Name);
                playerModelWidget.PlayerClass = PlayerClass.Male;
                playerModelWidget.CharacterSkinTexture = texture2;
                labelWidget5.Text = listItem.DisplayName;
                labelWidget6.Text = string.Format(LanguageControl.Get(_typeName, 4), texture2.Width, texture2.Height);
                if (listItem.IsBuiltIn)
                {
                    return containerWidget;
                }

                labelWidget6.Text += $" | {listItem.CreationTime.ToLocalTime():dd MMM yyyy HH:mm}";
                if (listItem.UseCount > 0)
                {
                    labelWidget6.Text += string.Format(LanguageControl.Get(_typeName, 2), listItem.UseCount);
                }
            }

            return containerWidget;
        };
    }

    public override void Enter(object[] parameters)
    {
        UpdateList();
    }

    public override void Leave()
    {
        _blocksTexturesCache.Clear();
        _characterSkinsCache.Clear();
    }

    public override void Update()
    {
        var selectedItem = (ListItem?)_contentList.SelectedItem;
        if (selectedItem == null)
        {
            return;
        }

        _deleteButton.IsEnabled = selectedItem is { IsBuiltIn: false };
        _uploadButton.IsEnabled = selectedItem is { IsBuiltIn: false };
        _filterLabel.Text = GetFilterDisplayName(_filter);
        if (_deleteButton.IsClicked)
        {
            var smallMessage = selectedItem.UseCount <= 0
                ? string.Format(LanguageControl.Get(_typeName, 5), selectedItem.DisplayName)
                : string.Format(LanguageControl.Get(_typeName, 6), selectedItem.DisplayName, selectedItem.UseCount);
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    LanguageControl.Get(_typeName, 9),
                    smallMessage,
                    LanguageControl.Get("Usual", "yes"), LanguageControl.Get("Usual", "no"),
                    delegate(MessageDialogButton button)
                    {
                        if (button != MessageDialogButton.Button1)
                        {
                            return;
                        }

                        ExternalContentManager.DeleteExternalContent(selectedItem.Type, selectedItem.Name);
                        UpdateList();
                    }
                )
            );
        }

        if (_uploadButton.IsClicked)
        {
            ExternalContentManager.ShowUploadUi(selectedItem.Type, selectedItem.Name);
        }

        if (_changeFilterButton.IsClicked)
        {
            var list = new List<ExternalContentType>
            {
                ExternalContentType.Unknown,
                ExternalContentType.BlocksTexture,
                ExternalContentType.CharacterSkin,
                ExternalContentType.FurniturePack
            };
            DialogsManager.ShowDialog(
                null,
                new ListSelectionDialog(
                    LanguageControl.Get(_typeName, 7),
                    list, 60f,
                    item => GetFilterDisplayName((ExternalContentType)item), delegate(object item)
                    {
                        if ((ExternalContentType)item == _filter)
                        {
                            return;
                        }

                        _filter = (ExternalContentType)item;
                        UpdateList();
                    }
                )
            );
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    private void UpdateList()
    {
        WorldsManager.UpdateWorldsList();
        var list = new List<ListItem>();
        if (_filter is ExternalContentType.BlocksTexture or ExternalContentType.Unknown)
        {
            BlocksTexturesManager.UpdateBlocksTexturesList();
            list.AddRange(BlocksTexturesManager.ReadOnlyBlockTexturesNames.Select(name2 => new ListItem
            {
                Name = name2,
                IsBuiltIn = BlocksTexturesManager.IsBuiltIn(name2),
                Type = ExternalContentType.BlocksTexture,
                DisplayName = BlocksTexturesManager.GetDisplayName(name2),
                CreationTime = BlocksTexturesManager.GetCreationDate(name2),
                UseCount = WorldsManager.WorldInfos.Count(wi => wi.WorldSettings.BlocksTextureName == name2)
            }));
        }

        if (_filter is ExternalContentType.CharacterSkin or ExternalContentType.Unknown)
        {
            CharacterSkinsManager.UpdateCharacterSkinsList();
            list.AddRange(CharacterSkinsManager.ReadOnlyCharacterSkinsNames.Select(name => new ListItem
            {
                Name = name,
                IsBuiltIn = CharacterSkinsManager.IsBuiltIn(name),
                Type = ExternalContentType.CharacterSkin,
                DisplayName = CharacterSkinsManager.GetDisplayName(name),
                CreationTime = CharacterSkinsManager.GetCreationDate(name),
                UseCount = WorldsManager.WorldInfos.Count(wi => wi.PlayerInfos.Any(pi => pi.CharacterSkinName == name))
            }));
        }

        if (_filter == ExternalContentType.FurniturePack || _filter == ExternalContentType.Unknown)
        {
            FurniturePacksManager.UpdateFurniturePacksList();
            list.AddRange(FurniturePacksManager.ReadOnlyFurniturePackNames.Select(furniturePackName => new ListItem
            {
                Name = furniturePackName,
                IsBuiltIn = false,
                Type = ExternalContentType.FurniturePack,
                DisplayName = FurniturePacksManager.GetDisplayName(furniturePackName),
                CreationTime = FurniturePacksManager.GetCreationDate(furniturePackName)
            }));
        }

        list.Sort(delegate(ListItem o1, ListItem o2)
        {
            if (o1.IsBuiltIn && !o2.IsBuiltIn)
            {
                return -1;
            }

            if (o2.IsBuiltIn && !o1.IsBuiltIn)
            {
                return 1;
            }

            if (string.IsNullOrEmpty(o1.Name) && !string.IsNullOrEmpty(o2.Name))
            {
                return -1;
            }

            return !string.IsNullOrEmpty(o1.Name) && string.IsNullOrEmpty(o2.Name)
                ? 1
                : string.CompareOrdinal(o1.DisplayName, o2.DisplayName);
        });
        _contentList.ClearItems();
        foreach (var item in list)
        {
            _contentList.AddItem(item);
        }
    }

    private static string GetFilterDisplayName(ExternalContentType filter)
    {
        return filter == ExternalContentType.Unknown
            ? LanguageControl.Get(_typeName, 8)
            : ExternalContentManager.GetEntryTypeDescription(filter);
    }

    private class ListItem
    {
        public DateTime CreationTime;

        public string DisplayName = string.Empty;

        public bool IsBuiltIn;

        public string Name = string.Empty;

        public ExternalContentType Type;

        public int UseCount;
    }
}
