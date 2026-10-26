namespace Game.Blocks;

public interface IEditableItemData
{
    IEditableItemData Copy();

    void LoadString(string data);

    string SaveString();
}
