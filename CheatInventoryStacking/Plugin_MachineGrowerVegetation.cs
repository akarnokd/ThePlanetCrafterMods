// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System.Collections;
using Unity.Netcode;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Disallow stacking in growers.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrowerVegetationHarvestable), nameof(MachineGrowerVegetationHarvestable.SetGrowerInventory))]
        static void Patch_MachineGrowerVegetationHarvestable_SetGrowerInventory(
            MachineGrowerVegetationHarvestable __instance,
            Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());

            __instance.StartCoroutine(MachineGrowerVegetationHarvestable_WaitForSecondInventory(__instance));
        }

        static IEnumerator MachineGrowerVegetationHarvestable_WaitForSecondInventory(
            MachineGrowerVegetationHarvestable __instance)
        {
            for (; ; )
            {
                var inv = fMachineGrowerVegetationHarvestableSecondInventory(__instance);
                if (inv == null)
                {
                    yield return null;
                }
                else
                {
                    noStackingInventories.Add(inv.GetId());
                    yield break;
                }
            }
        }

        /// <summary>
        /// Disallow stacking in outside growers.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrowerVegetationStatic), nameof(MachineGrowerVegetationStatic.SetGrowerInventory))]
        static void Patch_MachineGrowerVegetationStatic_SetGrowerInventory(Inventory inventory)
        {
            noStackingInventories.Add(inventory.GetId());
        }
    }
}
