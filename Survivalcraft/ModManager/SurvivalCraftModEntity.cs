using System.Xml.Linq;

using Engine.Media;

using Game.ContentReaders;

using StringReader = Game.ContentReaders.StringReader;

namespace Game.ModManager;

public class SurvivalCraftModEntity : ModEntity
{
    public SurvivalCraftModEntity()
    {
        var readers = new List<IContentReader>
        {
            new BitmapFontReader(),
            new DaeModelReader(),
            new ImageReader(),
            new JsonArrayReader(),
            new JsonObjectReader(),
            new ContentReaders.JsonModelReader(),
            new MtllibStructReader(),
            new ContentReaders.ObjModelReader(),
            new ShaderReader(),
            new SoundBufferReader(),
            new StreamingSourceReader(),
            new StringReader(),
            new SubtextureReader(),
            new Texture2DReader(),
            new XmlReader()
        };
        foreach (var reader in readers)
        {
            ContentManager.readerList.Add(reader.Type, reader);
        }

        var stream = Storage.OpenFile("app:Content.scpak", OpenFileMode.Read);
        var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        stream.Close();
        memoryStream.Position = 0L;
        memoryStream = GetDecipherStream(memoryStream);
        ResourcesMd5 = ModsManager.GetMd5(memoryStream.ToArray());
        ModArchive = ZipArchive.ZipArchive.Open(memoryStream, true);
        InitResources();
#if !SERVER
        LabelWidget.BitmapFont = ContentManager.Get<BitmapFont>("Fonts/Pericles");
#endif
    }

    public override bool IsSystemMod => true;

    public override void LoadBlocksData()
    {
        LoadingScreen.Info("加载方块数据:" + ModInfo.Name);
        BlocksManager.LoadBlocksData(ContentManager.Get<string>("BlocksData"));
        ContentManager.Dispose("BlocksData");
    }

    public override void LoadDll()
    {
        var blockTypes = new List<Type>();
        var types = typeof(BlocksManager).Assembly.GetTypes();
        foreach (var type in types)
        {
            if (type.IsSubclassOf(typeof(ModLoader)) && !type.IsAbstract)
            {
                if (Activator.CreateInstance(type) is ModLoader modLoader)
                {
                    modLoader.Entity = this;
                    modLoader.ModInitialize();
                    Loader = modLoader;
                    ModsManager.ModLoaders.Add(modLoader);
                }
            }

            if (type.IsSubclassOf(typeof(Block)) && !type.IsAbstract)
            {
                blockTypes.Add(type);
            }
        }

        foreach (var type in blockTypes)
        {
            var fieldInfo = type.GetRuntimeFields()
                .FirstOrDefault(p => p is { Name: "Index", IsPublic: true, IsStatic: true });
            if (fieldInfo == null || fieldInfo.FieldType != typeof(int))
            {
                ModsManager.AddException(new InvalidOperationException(
                    $"Block type \"{type.FullName}\" does not have static field Index of type int."));
            }
            else
            {
                var staticIndex = (int)fieldInfo.GetValue(null)!;
                var block = (Block)Activator.CreateInstance(type.GetTypeInfo().AsType())!;
                block.BlockIndex = staticIndex;
                Blocks.Add(block);
            }
        }
    }

    public override void LoadXdb(ref XElement? xElement)
    {
        LoadingScreen.Info("加载数据库:" + ModInfo.Name);
        xElement = ContentManager.Get<XElement>("Database");
        ContentManager.Dispose("Database");
    }

    public override void LoadCr(ref XElement xElement)
    {
        LoadingScreen.Info("加载合成谱:" + ModInfo.Name);
        xElement = ContentManager.Get<XElement>("CraftingRecipes");
        ContentManager.Dispose("CraftingRecipes");
    }

    public override void LoadClo(ClothingBlock block, ref XElement? xElement)
    {
        LoadingScreen.Info("加载衣物数据:" + ModInfo.Name);
        xElement = ContentManager.Get<XElement>("Clothes");
        ContentManager.Dispose("Clothes");
    }

    public override void SaveSettings(XElement xElement)
    {
    }

    public override void LoadSettings(XElement xElement)
    {
    }

    public override void OnBlocksInitialized()
    {
        BlocksManager.AddCategory("Terrain");
        BlocksManager.AddCategory("Plants");
        BlocksManager.AddCategory("Construction");
        BlocksManager.AddCategory("Items");
        BlocksManager.AddCategory("Tools");
        BlocksManager.AddCategory("Weapons");
        BlocksManager.AddCategory("Clothes");
        BlocksManager.AddCategory("Electrics");
        BlocksManager.AddCategory("Food");
        BlocksManager.AddCategory("Spawner Eggs");
        BlocksManager.AddCategory("Painted");
        BlocksManager.AddCategory("Dyed");
        BlocksManager.AddCategory("Fireworks");
    }

    private static MemoryStream GetDecipherStream(Stream stream)
    {
        var keepOpenStream = new MemoryStream();
        var buff = new byte[stream.Length];
        stream.ReadExactly(buff, 0, buff.Length);
        var hc = "再乱改就跑路，谁也别想玩！"u8.ToArray();
        var decipher = !hc.Where((t, i) => t != buff[i]).Any();

        if (decipher)
        {
            var buff2 = new byte[buff.Length - hc.Length];
            var k = 0;
            var t = 0;
            var l = (buff2.Length + 1) / 2;
            for (var i = 0; i < buff2.Length; i++)
            {
                if (i % 2 == 0)
                {
                    buff2[i] = buff[hc.Length + k];
                    k++;
                }
                else
                {
                    buff2[i] = buff[hc.Length + l + t];
                    t++;
                }
            }

            keepOpenStream.Write(buff2, 0, buff2.Length);
            keepOpenStream.Flush();
        }
        else
        {
            stream.Position = 0L;
            stream.CopyTo(keepOpenStream);
        }

        stream.Dispose();
        keepOpenStream.Position = 0L;
        return keepOpenStream;
    }
}
