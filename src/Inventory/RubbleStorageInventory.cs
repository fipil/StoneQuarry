﻿using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace StoneQuarry
{
    public class RubbleStorageInventory : InventoryGeneric
    {
        public IRockManager RockManager { get; }

        public AssetLocation? StoredRock
        {
            get
            {
                ItemSlot slot = FirstNonEmptySlot;
                if (slot != null)
                {
                    AssetLocation code = slot.Itemstack.Collectible.Code;
                    if (RockManager.TryResolveCode(code, out _, out AssetLocation? rock))
                    {
                        return rock;
                    }
                }
                return null;
            }
        }

        public RubbleStorageItemSlot StoneSlot => (RubbleStorageItemSlot)slots[0];
        public RubbleStorageItemSlot GravelSlot => (RubbleStorageItemSlot)slots[1];
        public RubbleStorageItemSlot SandSlot => (RubbleStorageItemSlot)slots[2];

        public int CurrentQuantity => StoneSlot.StackSize + GravelSlot.StackSize + SandSlot.StackSize;

        public int MaxStorable { get; }

        public float Filling => (float)CurrentQuantity / MaxStorable;

        public RubbleStorageInventory(ICoreAPI api, BlockPos? pos = null, int maxStorable = 0)
            : base(3, "SQ_RubbleStorage", pos?.ToString() ?? "-fake", api, OnNewSlot)
        {
            Pos = pos;
            MaxStorable = maxStorable;
            RockManager = api.ModLoader.GetModSystem<RockManager>();
        }

        public RubbleStorageItemSlot? GetSlotByType(string? type) => type switch
        {
            "stone" => StoneSlot,
            "gravel" => GravelSlot,
            "sand" => SandSlot,
            _ => null
        };

        public ItemStack? GetResource(string? type, int quantity)
        {
            ItemSlot? slot = GetSlotByType(type);

            if (slot == null || slot.StackSize == 0)
            {
                return null;
            }

            quantity = GameMath.Clamp(quantity, 0, slot.StackSize);
            quantity = GameMath.Min(quantity, slot.Itemstack.Collectible.MaxStackSize);

            return slot.TakeOut(quantity);
        }

        public bool TryAddResource(ItemSlot fromSlot, int quantity)
        {
            quantity = GameMath.Clamp(quantity, 0, MaxStorable - CurrentQuantity);

            if (fromSlot.Empty || quantity == 0)
            {
                return false;
            }

            AssetLocation code = fromSlot.Itemstack.Collectible.Code;
            if (RockManager.TryResolveCode(code, out string? contentType, out AssetLocation? rockName))
            {
                if (StoredRock == null || StoredRock.Equals(rockName))
                {
                    RubbleStorageItemSlot? slot = GetSlotByType(contentType);
                    if (slot != null)
                    {
                        return slot.TryGetFrom(fromSlot, quantity); ;
                    }
                }
            }
            return false;
        }

        public bool TryDegrade(string from, string to, bool split = true)
        {
            if (from == "stone" && StoneSlot.StackSize >= 2)
            {
                if (to == "gravel")
                {
                    GravelSlot.AddIn(1);
                }
                else if (to == "sand")
                {
                    float mpl = (float)StoneSlot.StackSize / GravelSlot.StackSize;
                    bool toSand = SandSlot.StackSize * mpl < GravelSlot.StackSize || !split;
                    if (toSand)
                    {
                        SandSlot.AddIn(1);
                    }
                    else
                    {
                        GravelSlot.AddIn(1);
                    }
                }
                else
                {
                    return false;
                }

                StoneSlot.TakeOut(2);
                return true;
            }


            if (from == "gravel" && to == "sand" && GravelSlot.StackSize > 0)
            {
                GravelSlot.TakeOut(1);
                SandSlot.AddIn(1);
                return true;
            }

            return false;
        }

        public override void MarkSlotDirty(int slotId)
        {
            base.MarkSlotDirty(slotId);
        }

        public override void FromTreeAttributes(ITreeAttribute treeAttribute)
        {
            base.FromTreeAttributes(treeAttribute);

            // Legacy v2.0.0-pre.5
            if (Empty)
            {
                string storedType = treeAttribute.GetString("storedType", null);
                if (storedType != null)
                {
                    AssetLocation rock = new(storedType);
                    IRockManager manager = Api.ModLoader.GetModSystem<RockManager>();

                    foreach (string type in new string[] { "stone", "gravel", "sand" })
                    {
                        int quantity = treeAttribute.GetInt(type, 0);
                        if (quantity > 0)
                        {
                            AssetLocation? code = manager.GetValue(rock, type);
                            if (code != null)
                            {
                                CollectibleObject? obj = Api.World.GetCollectibleObject(code);
                                if (obj != null)
                                {
                                    StoneSlot.Itemstack = new ItemStack(obj, quantity);
                                    StoneSlot.MarkDirty();
                                }
                            }
                        }
                    }
                }
            }
        }

        private static ItemSlot OnNewSlot(int slotId, InventoryGeneric self)
        {
            return new RubbleStorageItemSlot(slotId, (RubbleStorageInventory)self)
            {
                MaxSlotStackSize = int.MaxValue
            };
        }
    }
}
