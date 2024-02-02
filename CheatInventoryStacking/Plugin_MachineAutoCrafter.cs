// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using UnityEngine;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Conditionally stack in AutoCrafters.
        /// </summary>
        /// <param name="_inventory">The inventory of the AutoCrafter.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineAutoCrafter), nameof(MachineAutoCrafter.SetAutoCrafterInventory))]
        static void Patch_MachineAutoCrafter_SetAutoCrafterInventory(Inventory autoCrafterInventory)
        {
            if (!stackAutoCrafters.Value)
            {
                noStackingInventories.Add(autoCrafterInventory.GetId());
            }
        }

        /// <summary>
        /// The vanilla method uses isFull which might be unreliable with stacking enabled,
        /// thus we have to replace the coroutine with our fully rewritten one.
        /// 
        /// Consequently, the MachineAutoCrafter::CraftIfPossible consumes resources and
        /// does not verify the crafted item can be deposited into the local inventory, thus
        /// wasting the ingredients. Another reason to rewrite.
        /// </summary>
        /// <param name="__instance">The underlying MachineAutoCrafter instance to get public values from.</param>
        /// <param name="timeRepeat">How often to craft?</param>
        /// <param name="__result">The overridden coroutine</param>
        /// <returns>false when running with stack size > 1 and not multiplayer, true otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "TryToCraft")]
        static bool Patch_MachineAutoCrafter_TryToCraft_Patch(
            MachineAutoCrafter __instance,
            float timeRepeat,
            ref IEnumerator __result)
        {
            if (stackSize.Value > 1)
            {
                __result = MachineAutoCrafterTryToCraftOverride(__instance, timeRepeat);
                return false;
            }
            return true;
        }

        static IEnumerator MachineAutoCrafterTryToCraftOverride(MachineAutoCrafter __instance, float timeRepeat)
        {
            var wait = new WaitForSeconds(timeRepeat);

            var mSetItemsInRange = AccessTools.MethodDelegate<Action>(mMachineAutoCrafterSetItemsInRange, __instance);
            var mCraftIfPossible = AccessTools.MethodDelegate<Action<Group>>(mMachineAutoCrafterCraftIfPossible, __instance);
            var inv = fMachineAutoCrafterInventory(__instance);

            for (; ; )
            {
                if (fMachineAutoCrafterHasEnergy(__instance) && inv != null)
                {
                    var machineWo = __instance.GetComponent<WorldObjectAssociated>()?.GetWorldObject();
                    if (machineWo != null)
                    {
                        var linkedGroups = machineWo.GetLinkedGroups();
                        if (linkedGroups != null && linkedGroups.Count != 0)
                        {
                            var gr = linkedGroups[0];

                            if (!IsFullStackedOfInventory(inv, gr.id))
                            {
                                mSetItemsInRange();
                                mCraftIfPossible(gr);
                            }
                        }
                    }
                }
                yield return wait;
            }
        }

    }
}
