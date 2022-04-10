﻿using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace StoneQuarry
{
    public class BEPlugAndFeather : BlockEntity
    {
        public SimpleParticleProperties BreakParticles { get; private set; }

        /// <summary>
        /// All points for own plug network (including the current plug).
        /// Empty if the network does not exist.
        /// </summary>
        public List<BlockPos> Points { get; private set; } = new List<BlockPos>();
        public bool IsNetworkPart => Points.Count > 0;

        public List<AssetLocation> AllowedCodes => (Block as BlockPlugAndFeather).AllowedCodes;

        public int MaxWorkPerStage
        {
            get
            {
                int workNeeded = 5 + (Points.Count * 2);
                return (int)(workNeeded * Core.Config.PlugWorkModifier);
            }
        }

        private int _currentStageWork = 0;
        private bool _previewEnabled = false;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            BreakParticles = new SimpleParticleProperties()
            {
                MinQuantity = 5,
                AddQuantity = 2,
                MinSize = 1f,
                MaxSize = 3f,
                MinPos = new Vec3d(),
                AddPos = new Vec3d(1, 1, 1),
                MinVelocity = new Vec3f(0, -0.1f, 0),
                AddVelocity = new Vec3f(.2f, -0.2f, .2f),
                ColorByBlock = Block,
                LifeLength = .5f,
                ParticleModel = EnumParticleModel.Quad
            };
        }

        public bool IsDone(IWorldAccessor world)
        {
            foreach (var point in Points)
            {
                if (world.BlockAccessor.GetBlock(point) is BlockPlugAndFeather pointBlock)
                {
                    if (pointBlock.Stage != pointBlock.MaxStage)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool TryHitPlug(ItemStack byStack)
        {
            bool isPlugImpactTool = byStack?.ItemAttributes?.KeyExists("plugimpact") ?? false;
            if (IsNetworkPart && isPlugImpactTool)
            {
                _currentStageWork += byStack.ItemAttributes["plugimpact"].AsInt();
                if (_currentStageWork > MaxWorkPerStage)
                {
                    _currentStageWork -= MaxWorkPerStage;
                    return true;
                }
            }
            return false;
        }

        public void BreakAll(IWorldAccessor world, IServerPlayer byPlayer)
        {
            IDictionary<AssetLocation, int> quantitiesByRock = GetRocksInside(world, byPlayer);
            List<ItemStack> contentStacks = new List<ItemStack>();

            int rockQuantity = 0;
            foreach (var rock in quantitiesByRock)
            {
                rockQuantity += rock.Value;
                contentStacks.Add(new ItemStack(world.GetBlock(rock.Key), rock.Value));
            }

            string slabSize = null;

            if (rockQuantity >= 168) slabSize = "giant";
            else if (rockQuantity >= 126) slabSize = "huge";
            else if (rockQuantity >= 84) slabSize = "large";
            else if (rockQuantity >= 42) slabSize = "medium";
            else if (rockQuantity > 0) slabSize = "small";

            string dropItemString = "stoneslab-andesite-" + slabSize + "-north";

            AssetLocation dropItemLoc = new AssetLocation(Core.ModId, dropItemString);
            var dropItem = world.GetBlock(dropItemLoc) as BlockStoneSlab;
            if (dropItem != null)
            {
                ItemStack dropItemStack = new ItemStack(dropItem, 1);

                StoneSlabInventory.StacksToTreeAttributes(contentStacks, dropItemStack.Attributes, Api, dropItem.AllowedCodes);

                world.SpawnItemEntity(dropItemStack, GetInsideCube().Center.ToVec3d().Add(.5, .5, .5));
            }
            else
            {
                Api.Logger.Warning("[" + Core.ModId + "] Unknown drop item " + dropItemLoc);
            }
            world.BlockAccessor.BreakBlock(Pos, byPlayer);
        }

        public Dictionary<AssetLocation, int> GetRocksInside(IWorldAccessor world, IServerPlayer byPlayer)
        {
            var quantitiesByRock = new Dictionary<AssetLocation, int>();

            foreach (var pos in GetAllBlocksInside())
            {
                Block block = world.BlockAccessor.GetBlock(pos);
                if (AllowedCodes.Contains(block.Code))
                {
                    if (world.IsPlayerCanBreakBlock(pos, byPlayer))
                    {
                        BreakParticles.ColorByBlock = world.BlockAccessor.GetBlock(pos);
                        BreakParticles.MinPos = pos.ToVec3d();

                        world.BlockAccessor.SetBlock(0, pos);

                        world.SpawnParticles(BreakParticles, byPlayer);

                        if (quantitiesByRock.ContainsKey(block.Code))
                        {
                            quantitiesByRock[block.Code] += 1;
                        }
                        else
                        {
                            quantitiesByRock.Add(block.Code, 1);
                        }
                    }
                }
            }
            return quantitiesByRock;
        }

        public List<BlockPos> GetAllBlocksInside()
        {
            var blocks = new List<BlockPos>();

            Cuboidi cube = GetInsideCube();

            if (cube != null)
            {
                for (int x = cube.MinX; x <= cube.MaxX; x++)
                {
                    for (int y = cube.MinY; y <= cube.MaxY; y++)
                    {
                        for (int z = cube.MinZ; z <= cube.MaxZ; z++)
                        {
                            blocks.Add(new BlockPos(x, y, z));
                        }
                    }
                }
            }

            return blocks;
        }

        public Cuboidi GetInsideCube()
        {
            if (IsNetworkPart)
            {
                var cube = new Cuboidi(Points[0], Points[1]);
                cube.GrowBy(-1, -1, -1);

                foreach (var pos in Points)
                {
                    if (Api.World.BlockAccessor.GetBlock(pos) is BlockPlugAndFeather pb)
                    {
                        BlockPos innerPos = pos.Copy();

                        if (pb.Orientation == "down")
                        {
                            innerPos.Add(BlockFacing.DOWN.Normali);
                        }

                        if (pb.Orientation == "up")
                        {
                            innerPos.Add(BlockFacing.UP.Normali);
                        }

                        if (pb.Orientation == "horizontal")
                        {
                            innerPos.Add(BlockFacing.FromCode(pb.Direction).Normali);
                        }


                        if (innerPos.X < cube.X1) cube.X1 = innerPos.X;
                        if (innerPos.Y < cube.Y1) cube.Y1 = innerPos.Y;
                        if (innerPos.Z < cube.Z1) cube.Z1 = innerPos.Z;

                        if (innerPos.X > cube.X2) cube.X2 = innerPos.X;
                        if (innerPos.Y > cube.Y2) cube.Y2 = innerPos.Y;
                        if (innerPos.Z > cube.Z2) cube.Z2 = innerPos.Z;
                    }
                }

                return cube;
            }

            return null;
        }

        public Dictionary<string, int> FindRockCountsOld(IWorldAccessor world, List<BlockPos> blocks)
        {
            Dictionary<string, int> quantitiesByRock = new Dictionary<string, int>();

            foreach (var pos in blocks)
            {
                Block block = world.BlockAccessor.GetBlock(pos);
                string rockCode = block.FirstCodePart(1);
                if (block.FirstCodePart() == "rock")
                {
                    if (quantitiesByRock.ContainsKey(rockCode))
                    {
                        quantitiesByRock[rockCode] += 1;
                    }
                    else
                    {
                        quantitiesByRock.Add(rockCode, 1);
                    }
                }
            }
            return quantitiesByRock;
        }

        public void TogglePreview()
        {
            if (Api is ICoreClientAPI capi)
            {
                _previewEnabled = !_previewEnabled;

                var forPlayer = capi.World.Player;
                int highlightSlotId = 1312;

                if (_previewEnabled && IsNetworkPart)
                {
                    var blocks = GetAllBlocksInside();
                    if (blocks != null)
                    {
                        capi.World.HighlightBlocks(forPlayer, highlightSlotId, blocks);
                        return;
                    }
                }

                capi.World.HighlightBlocks(forPlayer, highlightSlotId, new List<BlockPos>());
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetInt("work", _currentStageWork);

            if (Points.Count != 0)
            {
                tree.SetInt("pointcount", Points.Count);
                for (int i = 0; i < Points.Count; i++)
                {
                    tree.SetBlockPos("point" + i, Points[i]);
                }
            }

            base.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            _currentStageWork = tree.GetInt("work", _currentStageWork);

            int slaveCount = tree.GetInt("pointcount", 0);
            if (slaveCount != 0)
            {
                for (int i = 0; i < slaveCount; i++)
                {
                    Points.Add(tree.GetBlockPos("point" + i));
                }
            }

            base.FromTreeAttributes(tree, worldAccessForResolve);
        }
    }
}