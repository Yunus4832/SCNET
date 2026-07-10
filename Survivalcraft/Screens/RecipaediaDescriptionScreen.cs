using System.Globalization;
using System.Xml.Linq;

namespace Game.Screens;

public class RecipaediaDescriptionScreen : Screen
{
    private const string _typeName = nameof(RecipaediaDescriptionScreen);

    private readonly BlockIconWidget _blockIconWidget;

    private readonly LabelWidget _descriptionWidget;

    private int _index;

    private readonly ButtonWidget _leftButtonWidget;

    private readonly LabelWidget _nameWidget;

    private readonly LabelWidget _propertyNames1Widget;

    private readonly LabelWidget _propertyNames2Widget;

    private readonly LabelWidget _propertyValues1Widget;

    private readonly LabelWidget _propertyValues2Widget;

    private readonly ButtonWidget _rightButtonWidget;

    private IList<int> _valuesList = [];

    public RecipaediaDescriptionScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/RecipaediaDescriptionScreen");
        LoadContents(this, node);
        _blockIconWidget = Children.Find<BlockIconWidget>("Icon")!;
        _nameWidget = Children.Find<LabelWidget>("Name")!;
        _leftButtonWidget = Children.Find<ButtonWidget>("Left")!;
        _rightButtonWidget = Children.Find<ButtonWidget>("Right")!;
        _descriptionWidget = Children.Find<LabelWidget>("Description")!;
        _propertyNames1Widget = Children.Find<LabelWidget>("PropertyNames1")!;
        _propertyValues1Widget = Children.Find<LabelWidget>("PropertyValues1")!;
        _propertyNames2Widget = Children.Find<LabelWidget>("PropertyNames2")!;
        _propertyValues2Widget = Children.Find<LabelWidget>("PropertyValues2")!;
    }

    public override void Enter(object[] parameters)
    {
        var item = (int)parameters[0];
        _valuesList = (IList<int>)parameters[1];
        _index = _valuesList.IndexOf(item);
        UpdateBlockProperties();
    }

    public override void Update()
    {
        _leftButtonWidget.IsEnabled = _index > 0;
        _rightButtonWidget.IsEnabled = _index < _valuesList.Count - 1;
        if (_leftButtonWidget.IsClicked || Input.Left)
        {
            _index = MathUtils.Max(_index - 1, 0);
            UpdateBlockProperties();
        }

        if (_rightButtonWidget.IsClicked || Input.Right)
        {
            _index = MathUtils.Min(_index + 1, _valuesList.Count - 1);
            UpdateBlockProperties();
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    private Dictionary<string, string> GetBlockProperties(int value)
    {
        var dictionary = new Dictionary<string, string>();
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        if (block.EmittedLightAmount > 0)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 1), block.EmittedLightAmount.ToString());
        }

        if (block.FuelFireDuration > 0f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 2),
                block.FuelFireDuration.ToString(CultureInfo.InvariantCulture));
        }

        dictionary.Add(LanguageManager.Get(_typeName, 3),
            block.MaxStacking > 1
                ? string.Format(LanguageManager.Get(_typeName, 24), block.MaxStacking.ToString())
                : LanguageManager.No);
        dictionary.Add(LanguageManager.Get(_typeName, 4),
            block.FireDuration > 0f ? LanguageManager.Yes : LanguageManager.No);
        if (block.GetNutritionalValue(value) > 0f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 5),
                block.GetNutritionalValue(value).ToString(CultureInfo.InvariantCulture));
        }

        if (block.GetRotPeriod(value) > 0)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 6),
                string.Format(LanguageManager.Get(_typeName, 25), $"{2 * block.GetRotPeriod(value) * 60f / 1200f:0.0}"));
        }

        if (block.DigMethod != BlockDigMethod.None)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 7),
                LanguageManager.Get("DigMethod", block.DigMethod.ToString()));
            dictionary.Add(LanguageManager.Get(_typeName, 8),
                block.DigResilience.ToString(CultureInfo.InvariantCulture));
        }

        if (block.ExplosionResilience > 0f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 9),
                block.ExplosionResilience.ToString(CultureInfo.InvariantCulture));
        }

        if (block.GetExplosionPressure(value) > 0f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 10),
                block.GetExplosionPressure(value).ToString(CultureInfo.InvariantCulture));
        }

        var flag = false;
        if (block.GetMeleePower(value) > 1f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 11),
                block.GetMeleePower(value).ToString(CultureInfo.InvariantCulture));
            flag = true;
        }

        if (block.GetMeleePower(value) > 1f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 12), $"{100f * block.GetMeleeHitProbability(value):0}%");
            flag = true;
        }

        if (block.GetProjectilePower(value) > 1f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 13),
                block.GetProjectilePower(value).ToString(CultureInfo.InvariantCulture));
            flag = true;
        }

        if (block.ShovelPower > 1f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 14), block.ShovelPower.ToString(CultureInfo.InvariantCulture));
            flag = true;
        }

        if (block.HackPower > 1f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 15), block.HackPower.ToString(CultureInfo.InvariantCulture));
            flag = true;
        }

        if (block.QuarryPower > 1f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 16), block.QuarryPower.ToString(CultureInfo.InvariantCulture));
            flag = true;
        }

        if (flag && block.Durability > 0)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 17), block.Durability.ToString());
        }

        if (block.ExperienceCount > 0f)
        {
            dictionary.Add(LanguageManager.Get(_typeName, 18),
                block.ExperienceCount.ToString(CultureInfo.InvariantCulture));
        }

        if (block is ClothingBlock)
        {
            var clothingData = BlocksManager.Blocks[Terrain.ExtractContents(value)]
                .GetClothingData(Terrain.ExtractData(value));
            dictionary.Add(LanguageManager.Get(_typeName, 19),
                clothingData.CanBeDyed ? LanguageManager.Yes : LanguageManager.No);
            dictionary.Add(LanguageManager.Get(_typeName, 20), $"{(int)(clothingData.ArmorProtection * 100f)}%");
            dictionary.Add(LanguageManager.Get(_typeName, 21),
                clothingData.Sturdiness.ToString(CultureInfo.InvariantCulture));
            dictionary.Add(LanguageManager.Get(_typeName, 22), $"{clothingData.Insulation:0.0} clo");
            dictionary.Add(LanguageManager.Get(_typeName, 23), $"{clothingData.MovementSpeedFactor * 100f:0}%");
        }

        return dictionary;
    }

    private void UpdateBlockProperties()
    {
        if (_index < 0 || _index >= _valuesList.Count)
        {
            return;
        }

        var value = _valuesList[_index];
        var num = Terrain.ExtractContents(value);
        var block = BlocksManager.Blocks[num];
        _blockIconWidget.Value = value;
        _nameWidget.Text = block.GetDisplayName(null, value);
        _descriptionWidget.Text = block.GetDescription(value);
        _propertyNames1Widget.Text = string.Empty;
        _propertyValues1Widget.Text = string.Empty;
        _propertyNames2Widget.Text = string.Empty;
        _propertyValues2Widget.Text = string.Empty;
        var blockProperties = GetBlockProperties(value);
        var num2 = 0;
        foreach (var item in blockProperties)
        {
            if (num2 < blockProperties.Count - blockProperties.Count / 2)
            {
                _propertyNames1Widget.Text = _propertyNames1Widget.Text + item.Key + ":\n";
                _propertyValues1Widget.Text = _propertyValues1Widget.Text + item.Value + "\n";
            }
            else
            {
                _propertyNames2Widget.Text = _propertyNames2Widget.Text + item.Key + ":\n";
                _propertyValues2Widget.Text = _propertyValues2Widget.Text + item.Value + "\n";
            }

            num2++;
        }
    }
}
