using Engine.Core;

using EntitySystem.TemplatesDatabase;

using Game;
using Game.Components;
using Game.Managers;
using Game.Network;
using Game.Network.Enums;
using Game.Subsystems;
using Game.Terrains;

namespace TerritoryStoneMod;

public sealed class TerritoryBlockBehavior : SubsystemBlockBehavior
{
    private TerritorySubsystem _territories = null!;
    private SubsystemModelsRenderer? _models;
    private DrawText? _drawText;

    public override int[] HandledBlocks => [TerritoryBlock.Index];

    public override void Load(ValuesDictionary valuesDictionary)
    {
        base.Load(valuesDictionary);
        _territories = Project.FindSubsystem<TerritorySubsystem>(true)!;
        _models = Project.FindSubsystem<SubsystemModelsRenderer>();
    }

    public override bool OnEditBlock(int x, int y, int z, int value, ComponentPlayer player)
    {
        if (!TerritoryBlock.IsTerritoryValue(value))
        {
            return false;
        }

        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return true;
        }

        RemoveDrawText();
        var territory = _territories.Store.Territories.FirstOrDefault(item =>
            item.StonePoint == new Point3(x, y, z));
        if (territory == null || territory.Owner != player.PlayerData.PlayerGUID)
        {
            player.ComponentGui.DisplaySmallMessage(LanguageManager.Get("TerritorySettings", "OwnerOnly"),
                Color.Yellow, false, true);
            return true;
        }

        player.ComponentGui.ModalPanelWidget = player.ComponentGui.ModalPanelWidget == null
            ? new TerritorySettingsWidget(_territories, territory, player,
                () => player.ComponentGui.ModalPanelWidget = null)
            : null;
        return true;
    }

    public override void OnHitByProjectile(CellFace cellFace, WorldItem worldItem)
    {
        if (CommonLib.WorkType == WorkType.Client || _models == null || worldItem.Value == 0 ||
            !TerritoryBlock.IsTerritoryValue(SubsystemTerrain.Terrain.GetCellValue(cellFace.X, cellFace.Y, cellFace.Z)))
        {
            return;
        }

        var block = BlocksManager.Blocks[Terrain.ExtractContents(worldItem.Value)];
        var ingredient = $"{block.CraftingId}:{Terrain.ExtractData(worldItem.Value)}";
        var text = string.Format(LanguageManager.Get("SubsystemTerritoryBlockBehavior", 5), ingredient);
        if (_drawText == null)
        {
            _drawText = _models.AddDrawText(new Vector3(cellFace.Point) + new Vector3(0.5f), text, Color.White);
        }
        else
        {
            _drawText.Text = text;
        }
    }

    public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) => RemoveDrawText();

    public override void Dispose() => RemoveDrawText();

    private void RemoveDrawText()
    {
        if (_drawText != null)
        {
            _models?.RemoveDrawText(_drawText);
            _drawText = null;
        }
    }
}
