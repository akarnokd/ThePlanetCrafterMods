using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Text;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        // prevent the reentrancy upon deleting an overfilled shredder.
        static bool suppressTryToCleanInventory;

        /// <summary>
        /// Conditionally disallow stacking in shredders.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineDestructInventoryIfFull), nameof(MachineDestructInventoryIfFull.SetDestructInventoryInventory))]
        static void MachineDestructInventoryIfFull_SetDestructInventoryInventory(Inventory _inventory)
        {
            if (!stackShredder.Value)
            {
                _noStackingInventories.Add(_inventory.GetId());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDestructInventoryIfFull), "TryToCleanInventory")]
        static bool MachineDestructInventoryIfFull_TryToCleanInventory(
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
