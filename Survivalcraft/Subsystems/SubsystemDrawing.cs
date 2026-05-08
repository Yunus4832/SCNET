using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemDrawing : Subsystem
{
    private readonly Dictionary<IDrawable, bool> _drawables = new();

    private readonly SortedMultiCollection<int, IDrawable> _sortedDrawables = new();

    public int DrawablesCount => _drawables.Count;

    private void AddDrawable(IDrawable drawable)
    {
        _drawables.Add(drawable, true);
    }

    private void RemoveDrawable(IDrawable drawable)
    {
        _drawables.Remove(drawable);
    }

    public void Draw(Camera camera)
    {
        _sortedDrawables.Clear();
        foreach (var key2 in _drawables.Keys)
        {
            var drawOrders = key2.DrawOrders;
            foreach (var key in drawOrders)
            {
                _sortedDrawables.Add(key, key2);
            }
        }

        foreach (var drawable in _sortedDrawables)
        {
            try
            {
                drawable.Value.Draw(camera, drawable.Key);
            }
            catch (Exception e)
            {
                Log.Error("Projectile draw error: " + e);
            }
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        foreach (var item in Project.FindSubsystems<IDrawable>())
        {
            AddDrawable(item);
        }
    }

    public override void OnEntityAdded(Entity entity)
    {
        foreach (var item in entity.FindComponents<IDrawable>())
        {
            if (item != null)
            {
                AddDrawable(item);
            }
        }
    }

    public override void OnEntityRemoved(Entity entity)
    {
        foreach (var item in entity.FindComponents<IDrawable>())
        {
            if (item != null)
            {
                RemoveDrawable(item);
            }
        }
    }
}
