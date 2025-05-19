// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System.Collections.Generic;
using UnityEngine;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), nameof(MachineDisintegrator.SetDisintegratorInventory))]
        static void Patch_MachineDisintegrator_SetDisintegratorInventory(Inventory inventory)
        {
            if (!stackOreCrusherIn.Value)
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), "SetSecondInventory")]
        static void Patch_MachineDisintegrator_SetSecondInventory(Inventory inventory)
        {
            if (!stackOreCrusherOut.Value)
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), "Start")]
        static void Patch_MachineDisintegrator_Start(
            InventoryAssociatedProxy ___secondInventoryAssociatedProxy)
        {
            if (!stackOreCrusherOut.Value)
            {
                ___secondInventoryAssociatedProxy.GetInventory((inv, _) =>
                {
                    noStackingInventories.Add(inv.GetId());
                });
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), "TryToDesintegrateAnObjectInInventory")]
        static bool Patch_MachineDisintegrator_TryToDesintegrateAnObjectInInventory(
            Inventory ____firstIventory,
            Inventory ____secondInventory,
            int ___giveXIngredientsBack
        )
        {
            if (!stackOreCrusherOut.Value || stackSize.Value <= 1)
            {
                return true;
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

                    Dictionary<string, int> groupCounts = [];

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
