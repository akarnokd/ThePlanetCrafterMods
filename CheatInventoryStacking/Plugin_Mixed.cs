using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Text;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Disallow stacking in growers.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrower), nameof(MachineGrower.SetGrowerInventory))]
        static void MachineGrower_SetGrowerInventory(Inventory _inventory)
        {
            _noStackingInventories.Add(_inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in outside growers.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOutsideGrower), nameof(MachineOutsideGrower.SetGrowerInventory))]
        static void MachineOutsideGrower_SetGrowerInventory(Inventory _inventory)
        {
            _noStackingInventories.Add(_inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in DNA/Incubator.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGenetics), nameof(UiWindowGenetics.SetGeneticsData))]
        static void UiWindowGenetics_SetGeneticsData(Inventory ___inventoryRight)
        {
            _noStackingInventories.Add(___inventoryRight.GetId());
        }

        /// <summary>
        /// Disallow stacking in butterfly farms.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineFlockSpawner), "Start")]
        static void MachineFlockSpawner_SetSpawnerInventory(
            MachineFlockSpawner __instance)
        {
            if (__instance.GetComponent<MachineGenerator>() == null)
            {
                if (__instance.TryGetComponent<InventoryAssociatedProxy>(out var iap))
                {
                    iap.GetInventory((inv, _) => _noStackingInventories.Add(inv.GetId()));
                }
            }
        }

        /// <summary>
        /// Conditionally disallow stackingin optimizers.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOptimizer), nameof(MachineOptimizer.SetOptimizerInventory))]
        static void MachineOptimizer_SetOptimizerInventory(Inventory _inventory)
        {
            if (!stackOptimizer.Value)
            {
                _noStackingInventories.Add(_inventory.GetId());
            }
        }
    }
}
