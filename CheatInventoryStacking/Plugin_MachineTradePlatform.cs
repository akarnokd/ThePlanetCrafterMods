// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        /// <summary>
        /// Conditionally disallow stacking in trade rockets.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineTradePlatform), nameof(MachineTradePlatform.SetInventoryTradePlatform))]
        static void Patch_MachineTradePlatform_SetInventoryTradePlatform(Inventory inventory)
        {
            if (!stackTradeRockets.Value)
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineTradePlatform), "OnTradeInventoryModified")]
        static bool Patch_MachineTradePlatform_OnTradeInventoryModified(
            MachineTradePlatform __instance,
            Inventory ____inventory)
        {
            if (stackTradeRockets.Value && stackSize.Value > 1)
            {
                if (__instance.GetComponent<SettingProxy>().GetSetting() == 1
                    && ____inventory.GetSize() * stackSize.Value <= ____inventory.GetInsideWorldObjects().Count)
                {
                    __instance.SendTradeRocket();
                }
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowTrade), "OnClickButtons")]
        static bool Patch_UiWindowTrade_OnClickButtons(
            UiWindowTrade __instance,
            MachineTradePlatform ____machineTradePlatform,
            Dictionary<Group, int> ____groupsWithNumber,
            UiGroupLine uiGroupLine, 
            Group group, 
            int changeOfValue)
        {
            if (stackSize.Value <= 1 || !stackTradeRockets.Value)
            {
                return true;
            }

            ____machineTradePlatform.GetMachineTradeInventory(inventory =>
            {
                var newCount = 0;
                if (changeOfValue < 0)
                {
                    if (____groupsWithNumber.TryGetValue(group, out var count))
                    {
                        newCount = Math.Max(0, count + changeOfValue);
                        ____groupsWithNumber[group] = newCount;
                    }
                    else
                    {
                        return;
                    }
                }
                else if (changeOfValue > 0)
                {
                    int stacks = 0;
                    int n = stackSize.Value;
                    int groupCurrent = 0;
                    foreach (var entry in ____groupsWithNumber)
                    {
                        var m = entry.Value;

                        if (entry.Key == group)
                        {
                            groupCurrent = m;
                        }
                        else
                        {
                            // first see how many full stacks this group occupies
                            stacks += m / n;
                            // check if incomplete stack exists because that counts as full stack use here
                            if (m % n != 0)
                            {
                                stacks++;
                            }
                        }
                    }

                    // the other items already make the inventory full, do nothing
                    if (stacks >= inventory.GetSize())
                    {
                        return;
                    }

                    groupCurrent += changeOfValue;

                    // how many stacks would the current group with the new amount use
                    int groupCurrentStackUse = groupCurrent / n;
                    if (groupCurrent % n != 0)
                    {
                        groupCurrentStackUse++;
                    }

                    // if it would create more stacks than the inventory capacity
                    // change the count to be the available free stacks times the stack size
                    if (stacks + groupCurrentStackUse > inventory.GetSize())
                    {
                        int freeStacks = inventory.GetSize() - stacks;
                        groupCurrent = freeStacks * n;
                    }
                    newCount = groupCurrent;
                    ____groupsWithNumber[group] = groupCurrent;
                }

                List<Group> toLink = [];
                foreach (var gc in ____groupsWithNumber)
                {
                    for (int i = 0; i < gc.Value; i++)
                    {
                        toLink.Add(gc.Key);
                    }
                }
                ____machineTradePlatform.SetMachineTradeLinkedGroups(toLink);
                uiGroupLine.UpdateQuantity(newCount);
                mUiWindowTradeUpdateTokenUi.Invoke(__instance, []);
            });

            return false;
        }

    }
}
