// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        // prevent the reentrancy upon deleting an overfilled shredder.
        static bool suppressTryToCleanInventory;

        /// <summary>
        /// Conditionally disallow stacking in shredders.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineDestructInventoryIfFull), nameof(MachineDestructInventoryIfFull.SetDestructInventoryInventory))]
        static void Patch_MachineDestructInventoryIfFull_SetDestructInventoryInventory(Inventory inventory)
        {
            if (!stackShredder.Value)
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDestructInventoryIfFull), "TryToCleanInventory")]
        static bool Patch_MachineDestructInventoryIfFull_TryToCleanInventory(
            MachineDestructInventoryIfFull __instance,
            WorldObject ____worldObject,
            Inventory ____inventory)
        {
            if (stackShredder.Value && stackSize.Value > 1)
            {
                if (!suppressTryToCleanInventory)
                {
                    try
                    {
                        suppressTryToCleanInventory = true;

                        // Instead of filling to the brim, we activate the auto cleanup when
                        // the number of stacks reaches the capacity, even if that means
                        // that technically the shredder is not full.
                        // This should prevent indefinite stalls because the inventory appears
                        // full in AddItem but the inventory may not fill the rest of the stack(s).

                        if (____worldObject != null
                            && ____worldObject.GetSetting() == 1
                            && ____inventory.GetSize() <= GetStackCount(____inventory.GetInsideWorldObjects())
                        )
                        {
                            InventoriesHandler.Instance.RemoveAndDestroyAllItemsFromInventory(____inventory);
                            __instance.actionnableInteractiveToAction?.OnActionInteractive();
                        }
                    }
                    finally
                    {
                        suppressTryToCleanInventory = false;
                    }
                }
                return false;
            }
            return true;
        }
    }
}
