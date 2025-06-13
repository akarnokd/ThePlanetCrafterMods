// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
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
        static readonly Dictionary<Group, int> recipeMap = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "GetInRange")]
        static bool Patch_MachineAutoCrafter_GetInRange(
            HashSet<WorldObject> ____worldObjectsInRange,
            List<ValueTuple<GameObject, Group>> ____gosInRangeForListing,
            List<InventoryAssociatedProxy> ____proxys,
            GameObject go, Group group
        )
        {
            if (stackSize.Value <= 1)
            {
                return true;
            }


            if (group is GroupItem)
            {
                if (NetworkManager.Singleton.IsServer && recipeMap.Count != 0)
                {
                    WorldObjectAssociated componentInChildren = go.GetComponentInChildren<WorldObjectAssociated>(true);
                    if (componentInChildren == null || (componentInChildren.GetWorldObject().GetGrowth() > 0f && componentInChildren.GetWorldObject().GetGrowth() < 100f))
                    {
                        return false;
                    }
                    if (recipeMap.TryGetValue(group, out var c))
                    {
                        ____worldObjectsInRange.Add(componentInChildren.GetWorldObject());
                        if (--c == 0)
                        {
                            recipeMap.Remove(group);
                        }
                        else
                        {
                            recipeMap[group] = c;
                        }
                    }
                }
                ____gosInRangeForListing.Add(new ValueTuple<GameObject, Group>(go, group));
                return false;
            }
            if (group.GetLogisticInterplanetaryType() != DataConfig.LogisticInterplanetaryType.Disabled && (go.GetComponentInChildren<InventoryAssociated>(true) != null || go.GetComponentInChildren<InventoryAssociatedProxy>(true) != null))
            {
                ____gosInRangeForListing.Add(new ValueTuple<GameObject, Group>(go, group));
                if (recipeMap.Count == 0)
                {
                    return false;
                }
                if (NetworkManager.Singleton.IsServer)
                {
                    ____proxys.Clear();
                    go.GetComponentsInChildren<InventoryAssociatedProxy>(true, ____proxys);
                    foreach (InventoryAssociatedProxy inventoryAssociatedProxy in ____proxys)
                    {
                        ValueTuple<WorldObject, int> requestedInventoryData = inventoryAssociatedProxy.GetRequestedInventoryData();
                        if (group.GetLogisticInterplanetaryType() != DataConfig.LogisticInterplanetaryType.EnabledOnSecondaryInventories || inventoryAssociatedProxy.GetSecondaryInventoryIndex() != -1)
                        {
                            foreach (WorldObject worldObject in InventoriesHandler.Instance.GetInventoryById(requestedInventoryData.Item2).GetInsideWorldObjects())
                            {
                                if (!worldObject.GetIsPlaced() || worldObject.GetGrowth() == 100f)
                                {
                                    var itemGroup = worldObject.GetGroup();
                                    if (recipeMap.TryGetValue(itemGroup, out var c))
                                    {
                                        ____worldObjectsInRange.Add(worldObject);
                                        if (--c == 0)
                                        {
                                            recipeMap.Remove(itemGroup);
                                            if (recipeMap.Count == 0)
                                            {
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            recipeMap[itemGroup] = c;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
        */
    }
}
