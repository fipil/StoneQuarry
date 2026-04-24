using StoneQuarry.Lib.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace StoneQuarry
{
    public class Core : ModSystem
    {
        public static string ModId => "stonequarryrepckfipil";
        public static string LegacyModId => "stonequarry";
        public PlugPreviewManager? PlugPreviewManager { get; private set; }

        public override void StartPre(ICoreAPI api)
        {
            var configs = api.ModLoader.GetModSystem<ConfigManager>();
            var config = configs.GetConfig<Config>();

            api.World.Config.SetInt($"{LegacyModId}:RubbleStorageMaxSize", config.RubbleStorageMaxSize);
            api.World.Config.SetInt($"{LegacyModId}:SlabStorageFlags", config.SlabStorageFlags);
            api.World.Config.SetInt($"{LegacyModId}:RubbleStorageStorageFlags", config.RubbleStorageStorageFlags);

            if (api is ICoreClientAPI capi)
            {
                PlugPreviewManager = new PlugPreviewManager(capi);
            }
        }

        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockStoneSlab", typeof(BlockStoneSlab));
            api.RegisterBlockEntityClass("StoneSlab", typeof(BEStoneSlab));

            api.RegisterBlockClass("BlockRubbleStorage", typeof(BlockRubbleStorage));
            api.RegisterBlockEntityClass("RubbleStorage", typeof(BERubbleStorage));

            api.RegisterBlockClass("BlockPlugAndFeather", typeof(BlockPlugAndFeather));
            api.RegisterBlockEntityClass("PlugAndFeather", typeof(BEPlugAndFeather));

            api.RegisterItemClass("ItemRubbleHammer", typeof(ItemRubbleHammer));
        }
    }
}
