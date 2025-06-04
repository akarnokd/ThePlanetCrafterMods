// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {

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


        /*
        static int optimizerLastFrame = -1;
        static MachineOptimizer optimizerLast = null;
        static readonly Dictionary<DataConfig.WorldUnitType, List<GameObject>> optimizerWorldUnitCache = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineOptimizer), "GetWorldObjectsOfMachines")]
        static bool Patch_MachineOptimizer_GetWorldObjectsOfMachines_Pre(
            MachineOptimizer __instance, 
            DataConfig.WorldUnitType worldUnitType,
            ref List<GameObject> __result)
        {
            if (__instance == optimizerLast && optimizerLastFrame == Time.frameCount)
            {
                return !optimizerWorldUnitCache.TryGetValue(worldUnitType, out __result);
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOptimizer), "GetWorldObjectsOfMachines")]
        static void Patch_MachineOptimizer_GetWorldObjectsOfMachines_Post(
            MachineOptimizer __instance,
            DataConfig.WorldUnitType worldUnitType,
            ref List<GameObject> __result)
        {
            optimizerLast = __instance;
            optimizerLastFrame = Time.frameCount;
            optimizerWorldUnitCache[worldUnitType] = __result;
        }
        */
    }
}
