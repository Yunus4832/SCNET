using System.Xml.Linq;
using Engine.Graphics;
using EntitySystem.TemplatesDatabase;

namespace Game.Screens;

public class BestiaryScreen : Screen
{
    private readonly ListPanelWidget _creaturesList;

    private Screen? _previousScreen;

    public BestiaryScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/BestiaryScreen");
        LoadContents(this, node);
        _creaturesList = Children.Find<ListPanelWidget>("CreaturesList")!;
        _creaturesList.ItemWidgetFactory = delegate(object item)
        {
            var bestiaryCreatureInfo2 = (BestiaryCreatureInfo)item;
            var node2 = ContentManager.Get<XElement>("Widgets/BestiaryItem");
            var obj = (ContainerWidget)LoadWidget(this, node2, null);
            var modelWidget = obj.Children.Find<ModelWidget>("BestiaryItem.Model")!;
            SetupBestiaryModelWidget(bestiaryCreatureInfo2, modelWidget,
                _creaturesList.Items.IndexOf(item) % 2 == 0 ? new Vector3(-1f, 0f, -1f) : new Vector3(1f, 0f, -1f),
                false, false);
            obj.Children.Find<LabelWidget>("BestiaryItem.Text")!.Text = bestiaryCreatureInfo2.DisplayName;
            obj.Children.Find<LabelWidget>("BestiaryItem.Details")!.Text = bestiaryCreatureInfo2.Description;
            return obj;
        };
        _creaturesList.ItemClicked += delegate(object item)
        {
            ScreensManager.SwitchScreen("BestiaryDescription", item,
                _creaturesList.Items.Cast<BestiaryCreatureInfo>().ToList());
        };
        var list = new List<BestiaryCreatureInfo>();
        foreach (var entitiesValuesDictionary in DatabaseManager.EntitiesValuesDictionaries)
        {
            var valuesDictionary =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary, typeof(ComponentCreature));
            if (valuesDictionary == null)
            {
                continue;
            }

            var value = valuesDictionary.GetValue<string>("DisplayName");
            if (value.StartsWith('[') && value.EndsWith("]"))
            {
                var lp = value.Substring(1, value.Length - 2)
                    .Split([":"], StringSplitOptions.RemoveEmptyEntries);
                value = LanguageControl.GetDatabase("DisplayName", lp[1]);
            }

            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var order = -1;
            var value2 = entitiesValuesDictionary.GetValue<ValuesDictionary>("CreatureEggData", false);
            var value3 = entitiesValuesDictionary.GetValue<ValuesDictionary>("Player", false);
            if (value2 == null && value3 == null)
            {
                continue;
            }

            if (value2 != null)
            {
                var value4 = value2.GetValue<int>("EggTypeIndex");
                if (value4 < 0)
                {
                    continue;
                }

                order = value4;
            }

            var valuesDictionary2 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentCreatureModel));
            var valuesDictionary3 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentBody));
            var valuesDictionary4 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentHealth));
            var valuesDictionary5 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentMiner));
            var valuesDictionary6 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentLocomotion));
            var valuesDictionary7 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentHerdBehavior));
            var valuesDictionary8 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentMount));
            var valuesDictionary9 =
                DatabaseManager.FindValuesDictionaryForComponent(entitiesValuesDictionary,
                    typeof(ComponentLoot));
            var dy = valuesDictionary.GetValue<string>("Description");
            if (dy.StartsWith('[') && dy.EndsWith(']'))
            {
                var lp = dy.Substring(1, dy.Length - 2)
                    .Split([":"], StringSplitOptions.RemoveEmptyEntries);
                dy = LanguageControl.GetDatabase("Description", lp[1]);
            }

            var bestiaryCreatureInfo = new BestiaryCreatureInfo
            {
                Order = order,
                DisplayName = value,
                Description = dy,
                ModelName = valuesDictionary2?.GetValue<string>("ModelName") ?? string.Empty,
                TextureOverride = valuesDictionary2?.GetValue<string>("TextureOverride") ?? string.Empty,
                Mass = valuesDictionary3?.GetValue<float>("Mass") ?? 0,
                AttackResilience = valuesDictionary4?.GetValue<float>("AttackResilience") ?? 0,
                AttackPower = valuesDictionary5?.GetValue<float>("AttackPower") ?? 0f,
                MovementSpeed = MathUtils.Max(valuesDictionary6?.GetValue<float>("WalkSpeed") ?? 0,
                    valuesDictionary6?.GetValue<float>("FlySpeed") ?? 0,
                    valuesDictionary6?.GetValue<float>("SwimSpeed") ?? 0),
                JumpHeight = MathUtils.Sqr(valuesDictionary6?.GetValue<float>("JumpSpeed") ?? 0) / 20f,
                IsHerding = valuesDictionary7 != null,
                CanBeRidden = valuesDictionary8 != null,
                HasSpawnerEgg = value2?.GetValue<bool>("ShowEgg") ?? false,
                Loot = valuesDictionary9 != null
                    ? ComponentLoot.ParseLootList(valuesDictionary9.GetValue<ValuesDictionary>("Loot"))
                    : []
            };
#if DEBUG
            if (bestiaryCreatureInfo.TextureOverride == "")
            {
                Log.Warning($"{bestiaryCreatureInfo.DisplayName}的模型纹理贴图为空");
            }

            if (bestiaryCreatureInfo.ModelName == "")
            {
                Log.Warning($"{bestiaryCreatureInfo.DisplayName}的模型为空");
            }

#endif
            if (value3 != null && entitiesValuesDictionary.DatabaseObject.Name.ToLower().Contains("female"))
            {
                bestiaryCreatureInfo.AttackPower *= 0.8f;
                bestiaryCreatureInfo.AttackResilience *= 0.8f;
                bestiaryCreatureInfo.MovementSpeed *= 1.03f;
                bestiaryCreatureInfo.JumpHeight *= MathUtils.Sqr(1.03f);
            }

            list.Add(bestiaryCreatureInfo);
        }

        foreach (var item in list.OrderBy(ci => ci.Order))
        {
            _creaturesList.AddItem(item);
        }
    }

    public override void Enter(object[] parameters)
    {
        if (ScreensManager.PreviousScreen != ScreensManager.FindScreen<Screen>("BestiaryDescription"))
        {
            _previousScreen = ScreensManager.PreviousScreen;
        }

        _creaturesList.SelectedItem = null;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_previousScreen);
        }
    }

    public static void SetupBestiaryModelWidget(BestiaryCreatureInfo info, ModelWidget modelWidget, Vector3 offset,
        bool autoRotate, bool autoAspect)
    {
        modelWidget.Model = ContentManager.Get<Model>(info.ModelName);
        modelWidget.TextureOverride = ContentManager.Get<Texture2D>(info.TextureOverride);
        var absoluteTransforms = new Matrix[modelWidget.Model.Bones.Count];
        modelWidget.Model.CopyAbsoluteBoneTransformsTo(absoluteTransforms);
        var boundingBox = modelWidget.Model.CalculateAbsoluteBoundingBox(absoluteTransforms);
        var x = MathUtils.Max(boundingBox.Size().X, 1.4f * boundingBox.Size().Y, boundingBox.Size().Z);
        modelWidget.ViewPosition = new Vector3(boundingBox.Center().X, 1.5f, boundingBox.Center().Z) +
                                   2.6f * MathUtils.Pow(x, 0.75f) * offset;
        modelWidget.ViewTarget = boundingBox.Center();
        modelWidget.ViewFov = 0.3f;
        modelWidget.AutoRotationVector = autoRotate
            ? new Vector3(0f, MathUtils.Clamp(1.7f / boundingBox.Size().Length(), 0.25f, 1.4f), 0f)
            : Vector3.Zero;
        if (!autoAspect)
        {
            return;
        }

        var num = MathUtils.Clamp(boundingBox.Size().XZ.Length() / boundingBox.Size().Y, 1f, 1.5f);
        modelWidget.Size = new Vector2(modelWidget.Size.Y * num, modelWidget.Size.Y);
    }
}
