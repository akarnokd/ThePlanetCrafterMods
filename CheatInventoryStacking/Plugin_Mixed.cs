using HarmonyLib;
using SpaceCraft;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Disallow stacking in growers.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrower), nameof(MachineGrower.SetGrowerInventory))]
        static void Patch_MachineGrower_SetGrowerInventory(Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in outside growers.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOutsideGrower), nameof(MachineOutsideGrower.SetGrowerInventory))]
        static void Patch_MachineOutsideGrower_SetGrowerInventory(Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in DNA/Incubator.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineConvertRecipe), nameof(MachineConvertRecipe.SetConverterInventory))]
        static void Patch_MachineConvertRecipe_SetConverterInventory(Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());
        }

        /// <summary>
        /// Disallow stacking in butterfly farms.
        /// </summary>
        /// <param name="_inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineFlockSpawner), "Start")]
        static void Patch_MachineFlockSpawner_SetSpawnerInventory(
            MachineFlockSpawner __instance)
        {
            if (__instance.GetComponent<MachineGenerator>() == null)
            {
                if (__instance.TryGetComponent<InventoryAssociatedProxy>(out var iap))
                {
                    iap.GetInventory((inv, _) => noStackingInventories.Add(inv.GetId()));
                }
            }
        }

        /// <summary>
        /// Conditionally disallow stackingin optimizers.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOptimizer), nameof(MachineOptimizer.SetOptimizerInventory))]
        static void Patch_MachineOptimizer_SetOptimizerInventory(Inventory inventory)
        {
            if (!stackOptimizer.Value)
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }
    }
}
