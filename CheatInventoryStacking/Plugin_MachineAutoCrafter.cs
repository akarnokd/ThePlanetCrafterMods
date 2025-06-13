// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.ConstrainedExecution;
using Unity.Netcode;
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
            yield return new WaitForSeconds((fMachineAutoCrafterInstancedAutocrafters() % 50) / 10f);

            var wait = new WaitForSeconds(timeRepeat);

            var mSetItemsInRange = AccessTools.MethodDelegate<Action>(mMachineAutoCrafterSetItemsInRange, __instance);
            var mCraftIfPossible = AccessTools.MethodDelegate<Action<Group>>(mMachineAutoCrafterCraftIfPossible, __instance);

            for (; ; )
            {
                if (fMachineAutoCrafterHasEnergy(__instance))
                {
                    fMachineAutoCrafterTimeHasCrafted(__instance) = Time.time;
                    var inv = fMachineAutoCrafterInventory(__instance);
                    if (inv != null)
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
                                    /*
                                    recipeMap.Clear();
                                    foreach (var g in gr.GetRecipe().GetIngredientsGroupInRecipe())
                                    {
                                        recipeMap.TryGetValue(g, out var c);
                                        recipeMap[g] = c + 1;
                                    }
                                    */
                                    mSetItemsInRange();

                                    // recipeMap.Clear();

                                    mCraftIfPossible(gr);
                                }
                            }
                        }
                    }
                }
                yield return wait;
            }
        }

        /*
        static DictionaryCounter usedUp = new(1024);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "CraftIfPossible")]
        static bool Patch_MachineAutoCrafter_CraftIfPossible(
            MachineAutoCrafter __instance,
            Group linkedGroup,
            HashSet<WorldObject> ____worldObjectsInRange,
            List<WorldObject> ____worldObjectsRecipe,
            ReadOnlyCollection<WorldObject> ____worldObjectsRecipeRO,
            Inventory ____autoCrafterInventory
            )
        {
            usedUp.Clear();
            var recipe = linkedGroup.GetRecipe().GetIngredientsGroupInRecipe();
            foreach (var item in recipe)
            {
                usedUp.Update(item.id);
            }

            ____worldObjectsRecipe.Clear();
            int remainingRecipe = recipe.Count;
            foreach (var wo in ____worldObjectsInRange)
            {
                if (usedUp.DeduceIfPositive(wo.GetGroup().id))
                {
                    remainingRecipe--;
                    ____worldObjectsRecipe.Add(wo);
                }
                if (remainingRecipe == 0)
                {
                    break;
                }
            }

            if (remainingRecipe == 0)
            {
                WorldObjectsHandler.Instance.DestroyWorldObjects(____worldObjectsRecipeRO, true);
                InventoriesHandler.Instance.AddItemToInventory(linkedGroup, ____autoCrafterInventory, null);
                if (__instance.gameObject.activeInHierarchy)
                {
                    __instance.CraftAnimation((GroupItem)linkedGroup, true);
                }
                CraftManager.AddOneToTotalCraft();
            }

            return false;
        }
        */
    }
}
