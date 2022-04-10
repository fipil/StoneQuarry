using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace StoneQuarry
{
    public class BlockRubbleStorage : Block, IMultiBlockMonolithicSmall
    {
        public static AssetLocation InteractSoundLocation => new AssetLocation("game", "sounds/block/heavyice");
        public static AssetLocation StoneCrushSoundLocation => new AssetLocation("game", "sounds/effect/stonecrush");
        public static AssetLocation WaterSplashSoundLocation => new AssetLocation("game", "sounds/environment/largesplash1");

        public BaseAllowedCodes AllowedCodes { get; private set; }

        public List<WorldInteraction[]> WorldInteractionsBySel { get; private set; }
        public SimpleParticleProperties InteractParticles { get; private set; }


        public Cuboidf[] MirroredCollisionBoxes;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);

            InteractParticles = new SimpleParticleProperties()
            {
                MinPos = new Vec3d(),
                AddPos = new Vec3d(.5, .5, .5),
                MinQuantity = 5,
                AddQuantity = 20,
                GravityEffect = .9f,
                WithTerrainCollision = true,
                ParticleModel = EnumParticleModel.Quad,
                LifeLength = 2.5f,
                MinVelocity = new Vec3f(-0.4f, -0.4f, -0.4f),
                AddVelocity = new Vec3f(0.8f, 1.2f, 0.8f),
                MinSize = 0.1f,
                MaxSize = 0.4f,
                DieOnRainHeightmap = false
            };

            AllowedCodes = new BaseAllowedCodes();
            AllowedCodes.FromJson(Attributes["allowedCodes"].ToString());

            if (api.Side == EnumAppSide.Client)
            {
                InitWorldInteractions();
            }

            MirroredCollisionBoxes = new Cuboidf[CollisionBoxes.Length];
            for (int i = 0; i < CollisionBoxes.Length; i++)
            {
                MirroredCollisionBoxes[i] = CollisionBoxes[i].RotatedCopy(0, 180, 0, new Vec3d(0.5, 0.5, 0.5));
            }
        }

        private void InitWorldInteractions()
        {
            var hammers = new List<ItemStack>();
            foreach (var colObj in api.World.Collectibles)
            {
                if (colObj.Attributes != null && colObj.Attributes["rubbleable"].Exists)
                {
                    hammers.Add(new ItemStack(colObj));
                }
            }

            var stacksByType = new Dictionary<string, List<ItemStack>> {
                { "sand", new List<ItemStack>() },
                { "gravel", new List<ItemStack>() },
                { "stone", new List<ItemStack>() }
            };

            foreach (var type in stacksByType.Keys)
            {
                foreach (var rock in AllowedCodes.Rocks)
                {
                    string code = AllowedCodes[rock, type];
                    var colObj = api.World.GetCollectibleObject(new AssetLocation(code));
                    if (colObj != null)
                    {
                        stacksByType[type].Add(new ItemStack(colObj));
                    }
                }
            }

            var waterPortion = api.World.GetItem(new AssetLocation("game:waterportion")).GetHandBookStacks(api as ICoreClientAPI);
            waterPortion[0].StackSize = 100; // 1 liter of liquid

            WorldInteractionsBySel = new List<WorldInteraction[]>() { new WorldInteraction[] {
                    new WorldInteraction() {
                        ActionLangCode = Core.ModId + ":wi-rubblestorage-hammer",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = hammers.ToArray()
                    },
                    new WorldInteraction() {
                        ActionLangCode = Core.ModId + ":wi-rubblestorage-add-one",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = stacksByType["stone"].ToArray(),
                        GetMatchingStacks = WIGetMatchingStacks_StoneType
                    },
                    new WorldInteraction() {
                        ActionLangCode = Core.ModId + ":wi-rubblestorage-add-stack",
                        MouseButton = EnumMouseButton.Right,
                        HotKeyCode = "sprint",
                        Itemstacks = stacksByType["stone"].Select((i)=>{
                            var r = i.Clone(); r.StackSize = r.Collectible.MaxStackSize; return r;
                        }).ToArray(),
                        GetMatchingStacks = WIGetMatchingStacks_StoneType
                    },
                    new WorldInteraction() {
                        ActionLangCode = Core.ModId + ":wi-rubblestorage-add-all",
                        MouseButton = EnumMouseButton.Right,
                        RequireFreeHand = true
                    },
                    new WorldInteraction()
                    {
                        ActionLangCode = Core.ModId + ":wi-rubblestorage-water",
                        MouseButton = EnumMouseButton.Right,
                        Itemstacks = waterPortion.ToArray()
                    }
                }};

            foreach (var type in stacksByType.Keys)
            {

                WorldInteractionsBySel.Add(new WorldInteraction[] {
                        new WorldInteraction()
                        {
                            ActionLangCode = Core.ModId + ":wi-rubblestorage-lock",
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = "sneak"
                        },
                        new WorldInteraction()
                        {
                            ActionLangCode = Core.ModId + ":wi-rubblestorage-take-one-" + type,
                            MouseButton = EnumMouseButton.Right,
                            Itemstacks = stacksByType[type].ToArray(),
                            GetMatchingStacks = WIGetMatchingStacks_StoneType
                        },
                        new WorldInteraction()
                        {
                            ActionLangCode = Core.ModId + ":wi-rubblestorage-take-stack-" + type,
                            MouseButton = EnumMouseButton.Right,
                            HotKeyCode = "sprint",
                            Itemstacks = stacksByType[type].Select((i)=>{
                                var r = i.Clone(); r.StackSize = r.Collectible.MaxStackSize; return r;
                            }).ToArray(),
                            GetMatchingStacks = WIGetMatchingStacks_StoneType
                        }
                    });
            }
        }

        private ItemStack[] WIGetMatchingStacks_StoneType(WorldInteraction wi, BlockSelection blockSel, EntitySelection entitySel)
        {
            var be = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BERubbleStorage;

            if (be?.StoredRock == null)
            {
                return wi.Itemstacks;
            }

            return wi.Itemstacks
                .Where((item) => AllowedCodes.HasCode(be.StoredRock, item.Collectible.Code.ToString()))
                .ToArray();
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            int stockQuantity = 1;

            if (byPlayer.Entity.Controls.Sprint)
            {
                stockQuantity = byPlayer.InventoryManager.ActiveHotbarSlot.MaxSlotStackSize;
            }

            if (!(world.BlockAccessor.GetBlockEntity(blockSel.Position) is BERubbleStorage be))
            {
                return true;
            }


            string selectedType = null;
            switch ((EnumStorageLock)blockSel.SelectionBoxIndex)
            {
                case EnumStorageLock.Stone: selectedType = "stone"; break;
                case EnumStorageLock.Gravel: selectedType = "gravel"; break;
                case EnumStorageLock.Sand: selectedType = "sand"; break;
            }


            var activeStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;

            // Player is looking at one of the buttons on the crate
            if (selectedType != null)
            {
                // Player try lock a resource type
                if (byPlayer.Entity.Controls.Sneak)
                {
                    if (be.StorageLock != (EnumStorageLock)blockSel.SelectionBoxIndex)
                    {
                        be.StorageLock = (EnumStorageLock)blockSel.SelectionBoxIndex;
                    }
                    else be.StorageLock = EnumStorageLock.None;
                }

                // Player try get a resource
                else if (be.TryRemoveResource(world, byPlayer, blockSel, selectedType, stockQuantity))
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                    }
                    world.PlaySoundAt(StoneCrushSoundLocation, byPlayer, byPlayer, true);

                    InteractParticles.MinPos = blockSel.Position.ToVec3d() + blockSel.HitPosition;
                    InteractParticles.ColorByBlock = world.BlockAccessor.GetBlock(blockSel.Position);
                    world.SpawnParticles(InteractParticles, byPlayer);
                }
            }

            // Player try use a tool or add a resource
            else if (activeStack != null)
            {
                // Rubble hammer
                if (activeStack.ItemAttributes?["rubbleable"]?.AsBool() ?? false)
                {
                    if (be.TryDegradeNext())
                    {
                        if (world.Side == EnumAppSide.Client)
                        {
                            (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                        }
                        world.PlaySoundAt(InteractSoundLocation, byPlayer, byPlayer, true, volume: 0.25f);

                        activeStack.Collectible.DamageItem(world, byPlayer.Entity, byPlayer.InventoryManager.ActiveHotbarSlot);
                    }
                }

                // Liquid container with water
                else if (activeStack.Attributes.GetTreeAttribute("contents")?.GetItemstack("0")?.Collectible.Code
                    .Equals(new AssetLocation("game:waterportion")) ?? false)
                {
                    if (be.TryDrench(world, blockSel, byPlayer))
                    {
                        if (world.Side == EnumAppSide.Client)
                        {
                            (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                        }
                        world.PlaySoundAt(WaterSplashSoundLocation, byPlayer, byPlayer, true);
                    }
                }

                // Resource
                else
                {
                    if (be.TryAddResource(byPlayer.InventoryManager.ActiveHotbarSlot, stockQuantity))
                    {
                        if (world.Side == EnumAppSide.Client)
                        {
                            (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                        }
                        world.PlaySoundAt(StoneCrushSoundLocation, byPlayer, byPlayer, true);
                    }
                }
            }

            // Player hands is empty, take all the matching blocks outs of inventory
            else if (blockSel.SelectionBoxIndex == 0 && activeStack == null)
            {

                if (be.TryAddAll(byPlayer))
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        (byPlayer as IClientPlayer).TriggerFpAnimation(EnumHandInteract.HeldItemAttack);
                    }
                    world.PlaySoundAt(StoneCrushSoundLocation, byPlayer, byPlayer, true);
                }
            }

            if (be.CurrentQuantity <= 0)
            {
                be.StoredRock = null;
            }

            be.CheckCurrentTop();

            return true;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack)
        {
            if (base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack))
            {
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BERubbleStorage be)
                {
                    if (byItemStack != null)
                    {
                        be.Content["stone"] = byItemStack.Attributes.GetInt("stone", 0);
                        be.Content["gravel"] = byItemStack.Attributes.GetInt("gravel", 0);
                        be.Content["sand"] = byItemStack.Attributes.GetInt("sand", 0);
                        be.StoredRock = byItemStack.Attributes.GetString("type", null);
                    }

                    be.CheckCurrentTop();

                    return true;
                }
            }

            return false;
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            string rockType = inSlot.Itemstack.Attributes.GetString("type");
            int stoneCount = inSlot.Itemstack.Attributes.GetInt("stone");
            int gravelCount = inSlot.Itemstack.Attributes.GetInt("gravel");
            int sandCount = inSlot.Itemstack.Attributes.GetInt("sand");

            if (string.IsNullOrEmpty(rockType)) rockType = Lang.Get(Core.ModId + ":info-rubblestorage-none");

            dsc.AppendLine(Lang.Get(Core.ModId + ":info-rubblestorage-type(type={0})", rockType));
            dsc.AppendLine(Lang.Get(Core.ModId + ":info-rubblestorage-stone(count={0})", stoneCount));
            dsc.AppendLine(Lang.Get(Core.ModId + ":info-rubblestorage-gravel(count={0})", gravelCount));
            dsc.AppendLine(Lang.Get(Core.ModId + ":info-rubblestorage-sand(count={0})", sandCount));
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection blockSel, IPlayer forPlayer)
        {
            return WorldInteractionsBySel[blockSel.SelectionBoxIndex].Append(base.GetPlacedBlockInteractionHelp(world, blockSel, forPlayer));
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            if (world.BlockAccessor.GetBlockEntity(pos) is BERubbleStorage be)
            {
                ItemStack dropstack = new ItemStack(world.BlockAccessor.GetBlock(pos));
                dropstack.Attributes.SetString("type", be.StoredRock);
                dropstack.Attributes.SetInt("stone", be.Content["stone"]);
                dropstack.Attributes.SetInt("gravel", be.Content["gravel"]);
                dropstack.Attributes.SetInt("sand", be.Content["sand"]);
                return new ItemStack[] { dropstack };
            }
            return null;
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
        {
            if (blockAccessor.GetBlockEntity(pos) is BERubbleStorage be)
            {
                Cuboidf[] collision = new Cuboidf[CollisionBoxes.Length];
                CollisionBoxes.CopyTo(collision, 0);
                collision[0].Y2 = 0.8f - 0.06f * be.CurrentContentLevel;
                return collision;
            }

            return base.GetCollisionBoxes(blockAccessor, pos);
        }

        public Cuboidf[] MBGetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            if (blockAccessor.GetBlockEntity(pos + offset.AsBlockPos) is BERubbleStorage be)
            {
                Cuboidf[] collision = new Cuboidf[MirroredCollisionBoxes.Length];
                MirroredCollisionBoxes.CopyTo(collision, 0);
                collision[0].Y2 = 0.8f - 0.06f * be.CurrentContentLevel;
                return collision;
            }

            return MirroredCollisionBoxes;
        }

        public Cuboidf[] MBGetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos, Vec3i offset)
        {
            return new Cuboidf[] { Cuboidf.Default() };
        }
    }
}