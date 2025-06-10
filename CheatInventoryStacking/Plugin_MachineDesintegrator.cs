﻿// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), nameof(MachineDisintegrator.SetDisintegratorInventory))]
        static void Patch_MachineDisintegrator_SetDisintegratorInventory(
            MachineDisintegrator __instance,
            Inventory inventory
        )
        {
            if (!stackOreCrusherIn.Value)
            {
                noStackingInventories.Add(inventory.GetId());
            }
            if (!stackOreCrusherOut.Value)
            {
                me.StartCoroutine(MachineDisintegrator_WaitForSecondInventory(__instance));
            }
        }

        static IEnumerator MachineDisintegrator_WaitForSecondInventory(
            MachineDisintegrator __instance)
        {
            bool requesting = false;
            while (fMachineDisintegratorSecondInventory(__instance) == null)
            {
                if (__instance.secondInventoryAssociatedProxy.IsSpawned)
                {
                    if (!requesting)
                    {
                        requesting = true;
                        __instance.secondInventoryAssociatedProxy.GetInventory((inv, _) =>
                        {
                            fMachineDisintegratorSecondInventory(__instance) = inv;
                        });
                    }
                }
                yield return null;
            }

            var inv = fMachineDisintegratorSecondInventory(__instance);
            noStackingInventories.Add(inv.GetId());
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), "TryToDesintegrateAnObjectInInventory")]
        static bool Patch_MachineDisintegrator_TryToDesintegrateAnObjectInInventory(
            Inventory ____firstIventory,
            ref Inventory ____secondInventory,
            int ___giveXIngredientsBack
        )
        {
            if (!stackOreCrusherOut.Value || stackSize.Value <= 1)
            {
                return true;
            }

            if (!stackOreCrusherOut.Value)
            {
                noStackingInventories.Add(____secondInventory.GetId());
            }

            foreach (var wo in ____firstIventory.GetInsideWorldObjects())
            {
                var recipe = wo.GetGroup().GetRecipe().GetIngredientsGroupInRecipe();
                if (recipe.Count != 0)
                {
                    var list = new List<Group>(recipe);
                    var candidates = new List<Group>();

                    for (int i = 0; i < ___giveXIngredientsBack; i++)
                    {
                        int num = Random.Range(0, list.Count);
                        candidates.Add(list[num]);
                        list.RemoveAt(num);
                        if (list.Count == 0)
                        {
                            break;
                        }
                    }

                    groupCounts.Clear();

                    int n = stackSize.Value;
                    int stacks = 0;

                    foreach (var worldObject in ____secondInventory.GetInsideWorldObjects())
                    {
                        AddToStack(GeneticsGrouping.GetStackId(worldObject), groupCounts, n, ref stacks);
                    }

                    foreach (var candidate in candidates)
                    {
                        AddToStack(candidate.GetId(), groupCounts, n, ref stacks);
                    }

                    if (stacks <= ____secondInventory.GetSize())
                    {
                        InventoriesHandler.Instance.RemoveItemFromInventory(wo, ____firstIventory, true, null);

                        foreach (var candidate in candidates)
                        {
                            InventoriesHandler.Instance.AddItemToInventory(candidate, ____secondInventory, null);
                        }

                        return false;
                    }
                }
            }

            return false;
        }
    }
}
