﻿using System;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace StoneQuarry
{
    public class Core : ModSystem
    {
        public static Config Config { get; private set; }
        public static string ModId { get; private set; }
        public static ILogger ModLogger { get; private set; }

        ICoreAPI api;

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;

            ModId = Mod.Info.ModID;
            ModLogger = Mod.Logger;

            LoadConfig();
            SetConfigForPatches();
        }

        public override void Start(ICoreAPI api)
        {
            ClassRegister();
        }

        private void LoadConfig()
        {
            string configFilename = ModId + ".json";
            if (!File.Exists(api.GetOrCreateDataPath("ModConfig") + "/" + configFilename))
            {
                Config = new Config();
            }
            else
            {
                try
                {
                    Config = api.LoadModConfig<Config>(configFilename);
                }
                catch (Exception e)
                {
                    ModLogger.Error($"Config file cannot be loaded, a new one will be created. Error message: {e.Message}");
                    Config = new Config();
                }
            }
            api.StoreModConfig(Config, configFilename);
        }

        private void SetConfigForPatches()
        {
            foreach (var field in typeof(PlugSizes).GetFields())
            {
                int value = (int)field.GetValue(Config.PlugSizes);
                api.World.Config.SetInt($"SQ_PlugSizes_{field.Name}", value);
            }

            foreach (var field in typeof(PlugSizesMoreMetals).GetFields())
            {
                int value = (int)field.GetValue(Config.PlugSizesMoreMetals);
                api.World.Config.SetInt($"SQ_PlugSizesMoreMetals_{field.Name}", value);
            }

            api.World.Config.SetInt($"SQ_RubbleStorageMaxSize", Config.RubbleStorageMaxSize);
        }

        private void ClassRegister()
        {
            api.RegisterBlockClass("BlockStoneSlab", typeof(BlockStoneSlab));
            api.RegisterBlockEntityClass("StoneSlab", typeof(BEStoneSlab));
            api.RegisterItemClass("ItemSlabTool", typeof(ItemSlabTool));

            api.RegisterBlockClass("BlockRubbleStorage2", typeof(BlockRubbleStorage));
            api.RegisterBlockEntityClass("RubbleStorage2", typeof(BERubbleStorage));

            api.RegisterBlockClass("BlockPlugAndFeather", typeof(BlockPlugAndFeather));
            api.RegisterBlockEntityClass("PlugAndFeather", typeof(BEPlugAndFeather));
        }
    }
}
