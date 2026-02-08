using System;
using System.Collections.Generic;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace HoneyBatchSqueeze;

[HarmonyPatch(typeof(CollectibleBehaviorSqueezable), nameof(CollectibleBehaviorSqueezable.OnHeldInteractStop))]
public static class CollectibleBehaviorSqueezablePatch {
    private static readonly AssetLocation HoneyCombCode = new("game:honeycomb");

    static bool Prefix(
        CollectibleBehaviorSqueezable __instance,
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandling handling
    ) {
        // Only intercept honeycomb items
        if (slot.Itemstack?.Collectible?.Code != HoneyCombCode) {
            return true; // Not honeycomb, let original handle it
        }

        byEntity.StopAnimation(__instance.AnimationCode);

        if (blockSel == null) {
            return true; // Let original handle base call
        }

        Block block = byEntity.World.BlockAccessor.GetBlock(blockSel.Position);
        if (!__instance.CanSqueezeInto(byEntity.World, block, blockSel)) {
            return true; // Let original handle base call
        }

        handling = EnumHandling.PreventDefault;

        float requiredTime = __instance.SqueezeTime - 0.05f;
        if (secondsUsed < requiredTime || __instance.SqueezedLiquid == null || byEntity.World.Side == EnumAppSide.Client) {
            return false; // Skip original, we've set handling
        }

        IWorldAccessor world = byEntity.World;
        IPlayer player = null;
        if (byEntity is EntityPlayer entityPlayer) {
            player = world.PlayerByUid(entityPlayer.PlayerUID);
        }

        int squeezedCount = 0;

        while (slot.StackSize > 0 && __instance.CanSqueezeInto(world, block, blockSel)) {
            ItemStack liquidStack = new ItemStack(__instance.SqueezedLiquid, 99999);

            bool squeezed = false;

            if (block is BlockLiquidContainerTopOpened containerTopOpened) {
                if (containerTopOpened.TryPutLiquid(blockSel.Position, liquidStack, __instance.SqueezedLitres) > 0) {
                    squeezed = true;
                }
            }
            else if (block is BlockBarrel blockBarrel) {
                if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBarrel barrelEntity) {
                    if (!barrelEntity.Sealed && blockBarrel.TryPutLiquid(blockSel.Position, liquidStack, __instance.SqueezedLitres) > 0) {
                        squeezed = true;
                    }
                }
            }
            else if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGroundStorage groundStorage) {
                ItemSlot slotAt = groundStorage.GetSlotAt(blockSel);
                if (slotAt?.Itemstack?.Block is BlockLiquidContainerTopOpened groundContainer && __instance.CanSqueezeInto(world, groundContainer, null)) {
                    if (groundContainer.TryPutLiquid(slotAt.Itemstack, liquidStack, __instance.SqueezedLitres) > 0) {
                        squeezed = true;
                        groundStorage.MarkDirty(true);
                    }
                }
            }

            if (!squeezed) {
                break; // Container is full or something went wrong
            }

            slot.TakeOut(1);
            squeezedCount++;

            // Give return stacks (beeswax etc.)
            JsonItemStack[] returnStacks = __instance.ReturnStacks;
            if (returnStacks != null) {
                foreach (JsonItemStack returnStack in returnStacks) {
                    ItemStack itemstack = returnStack.ResolvedItemstack?.Clone();
                    if (itemstack == null) continue;

                    if (player == null || !player.InventoryManager.TryGiveItemstack(itemstack)) {
                        world.SpawnItemEntity(itemstack, blockSel.Position);
                    }
                }
            }
        }

        if (squeezedCount > 0) {
            slot.MarkDirty();
        }

        // Always skip original - we've handled everything including the handling flag
        return false;
    }
}