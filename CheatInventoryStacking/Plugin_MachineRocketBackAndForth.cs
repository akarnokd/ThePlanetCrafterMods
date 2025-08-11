// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {

        static bool IsBackAndForthStackable(MachineRocketBackAndForth __instance)
        {
            return (__instance is MachineRocketBackAndForthTrade && stackTradeRockets.Value)
                || (__instance is MachineRocketBackAndForthInterplanetaryExchange && stackInterplanetaryRockets.Value);
        }

        /// <summary>
        /// Conditionally disallow stacking in trade rockets.
        /// </summary>
        /// <param name="inventory"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineRocketBackAndForth), nameof(MachineRocketBackAndForth.SetInventoryRocketBackAndForth))]
        static void Patch_MachineRocketBackAndForth_SetInventoryRocketBackAndForth(
            MachineRocketBackAndForth __instance,
            Inventory inventory)
        {
            if (!IsBackAndForthStackable(__instance))
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineRocketBackAndForth), "OnRocketBackAndForthInventoryModified")]
        static bool Patch_MachineRocketBackAndForth_OnRocketBackAndForthInventoryModified(
            MachineRocketBackAndForth __instance,
            Inventory ____inventory)
        {
            if (stackSize.Value > 1 && IsBackAndForthStackable(__instance))
            {
                if (__instance.GetComponent<SettingProxy>().GetSetting() == 1
                    && ____inventory.GetSize() * stackSize.Value <= fInventoryWorldObjectsInInventory(____inventory).Count)
                {
                    __instance.SendBackAndForthRocket();
                }
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowTrade), "OnClickButtons")]
        static bool Patch_UiWindowTrade_OnClickButtons(
            UiWindowTrade __instance,
            int ____inventorySize,
            MachineRocketBackAndForth ____machineRocketBackAndForth,
            Dictionary<Group, int> ____groupsWithNumber,
            UiGroupLine uiGroupLine, 
            Group group, 
            int changeOfValue)
        {
            if (stackSize.Value <= 1 || !IsBackAndForthStackable(____machineRocketBackAndForth))
            {
                return true;
            }

            if (____inventorySize <= 0)
            {
                return false;
            }
            var toChange = changeOfValue;
            if (Keyboard.current[Key.LeftShift].isPressed || Keyboard.current[Key.RightShift].isPressed)
            {
                toChange *= 10;
            }
            if (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed)
            {
                toChange *= 10;
            }

            var newCount = 0;
            if (toChange < 0)
            {
                if (____groupsWithNumber.TryGetValue(group, out var count))
                {
                    newCount = Math.Max(0, count + toChange);
                    ____groupsWithNumber[group] = newCount;
                }
                else
                {
                    return false;
                }
            }
            else if (toChange > 0)
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
                if (stacks >= ____inventorySize)
                {
                    return false;
                }

                groupCurrent += toChange;

                // how many stacks would the current group with the new amount use
                int groupCurrentStackUse = groupCurrent / n;
                if (groupCurrent % n != 0)
                {
                    groupCurrentStackUse++;
                }

                // if it would create more stacks than the inventory capacity
                // change the count to be the available free stacks times the stack size
                if (stacks + groupCurrentStackUse > ____inventorySize)
                {
                    int freeStacks = ____inventorySize - stacks;
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
            ____machineRocketBackAndForth.SetMachineRocketBackAndForthLinkedGroups(toLink);
            uiGroupLine.UpdateQuantity(newCount);
            mUiWindowTradeUpdateTokenUi.Invoke(__instance, []);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowInterplanetaryExchange), "OnClickButtons")]
        static bool Patch_UiWindowInterplanetaryExchange_OnClickButtons(
            int ____inventorySize,
            MachineRocketBackAndForthInterplanetaryExchange ____machineRocketBackAndForthInterplanetary,
            Dictionary<Group, int> ____groupsWithNumber,
            UiGroupLine uiGroupLine,
            Group group,
            int changeOfValue,
            PlanetData ____destinationPlanet
        )
        {
            if (stackSize.Value <= 1 || !IsBackAndForthStackable(____machineRocketBackAndForthInterplanetary))
            {
                return true;
            }

            if (____inventorySize <= 0)
            {
                return false;
            }

            var toChange = changeOfValue;
            if (Keyboard.current[Key.LeftShift].isPressed || Keyboard.current[Key.RightShift].isPressed)
            {
                toChange *= 10;
            }
            if (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed)
            {
                toChange *= 10;
            }

            var newCount = 0;
            if (toChange < 0)
            {
                if (____groupsWithNumber.TryGetValue(group, out var count))
                {
                    newCount = Math.Max(0, count + toChange);
                    ____groupsWithNumber[group] = newCount;
                }
                else
                {
                    return false;
                }
            }
            else if (toChange > 0)
            {
                ____groupsWithNumber.TryGetValue(group, out var currentAmount);
                var availableAmount = LogisticInterplanetaryManager.Instance.GetGroupCount(group, ____destinationPlanet.GetPlanetHash());
                if (currentAmount + toChange > availableAmount)
                {
                    toChange = availableAmount - currentAmount;
                }
                if (toChange <= 0)
                {
                    return false;
                }
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
                if (stacks >= ____inventorySize)
                {
                    return false;
                }

                groupCurrent += toChange;

                // how many stacks would the current group with the new amount use
                int groupCurrentStackUse = groupCurrent / n;
                if (groupCurrent % n != 0)
                {
                    groupCurrentStackUse++;
                }

                // if it would create more stacks than the inventory capacity
                // change the count to be the available free stacks times the stack size
                if (stacks + groupCurrentStackUse > ____inventorySize)
                {
                    int freeStacks = ____inventorySize - stacks;
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
            ____machineRocketBackAndForthInterplanetary.SetMachineRocketBackAndForthLinkedGroups(toLink);
            uiGroupLine.UpdateQuantity(newCount);

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowInterplanetaryExchange), "UpdateUiForSelectedItems")]
        static void Patach_UiWindowInterplanetaryExchange_UpdateUiForSelectedItems(
            int ____inventorySize,
            TextMeshProUGUI ___textInventorySpace
        )
        {
            if (stackSize.Value <= 1 || !stackInterplanetaryRockets.Value)
            {
                return;
            }
            var txt = ___textInventorySpace.text;
            var idx = txt.IndexOf('/');
            if (idx > 0)
            {
                ___textInventorySpace.text = txt[..idx] + "/ " + (____inventorySize * stackSize.Value);
            }
        }

        static Color defaultBackgroundColor = new(0.25f, 0.25f, 0.25f, 0.9f);
        static Color defaultTextColor = new(1f, 1f, 1f, 1f);

        static readonly Dictionary<string, int> groupCountsCache = [];
        static readonly List<Group> groupSetCache = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GroupList), nameof(GroupList.AddGroups), [typeof(List<SpaceCraft.Group>), typeof(GroupInfosDisplayerBlocksSwitches), typeof(bool)])]
        static bool Patch_GroupList_AddGroups(GroupList __instance,
            List<Group> _groups,
            bool _showBacklines,
            GameObject ___imageMore,
            int ___showMoreAt,
            GameObject ___grid,
            List<GroupDisplayer> ___groupsDisplayer,
            GroupInfosDisplayerBlocksSwitches _infosDisplayerGroup,
            GameObject ___groupDisplayerGameObject
        )
        {
            if (__instance.GetComponentInParent<UiWindowInterplanetaryExchange>() == null
                || stackSize.Value <= 1
                || !stackInterplanetaryRockets.Value
                || !groupListEnabled.Value)
            {
                return true;
            }
            var idg = _infosDisplayerGroup ?? new GroupInfosDisplayerBlocksSwitches();

            groupCountsCache.Clear();
            groupSetCache.Clear();

            foreach (var gr in _groups)
            {
                if (!groupCountsCache.TryGetValue(gr.id, out var cnt))
                {
                    groupSetCache.Add(gr);
                }
                groupCountsCache[gr.id] = cnt + 1;
            }

            for (int i = 0; i < groupSetCache.Count; i++)
            {
                Group _group = groupSetCache[i];
                var count = groupCountsCache[_group.id];

                if (___showMoreAt > 0 && ___groupsDisplayer.Count == ___showMoreAt)
                {
                    GameObject obj = Instantiate(___imageMore, ___grid.transform);
                    obj.GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
                    obj.SetActive(value: true);
                }

                GameObject gameObject = Instantiate(___groupDisplayerGameObject, ___grid.transform);
                gameObject.GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
                GroupDisplayer component = gameObject.GetComponent<GroupDisplayer>();
                component.SetGroupAndUpdateDisplay(_group, greyed: false, showName: false, isLocked: false, _showBacklines);
                ___groupsDisplayer.Add(component);
                gameObject.AddComponent<EventHoverShowGroup>().SetHoverGroupEvent(_group, idg, default, null, null);
                EventsHelpers.AddTriggerEvent(gameObject, EventTriggerType.PointerClick, 
                    evt =>
                    {
                        mGroupListOnGroupClicked.Invoke(__instance, [evt]);
                    }, 
                    new EventTriggerCallbackData(_group));

                if (count > 1)
                {
                    int fs = groupListFontSize.Value;
                    var countBackground = new GameObject("GroupListStackBackground");
                    countBackground.transform.SetParent(gameObject.transform);

                    Image image = countBackground.AddComponent<Image>();
                    image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

                    var rectTransform = image.GetComponent<RectTransform>();
                    rectTransform.localPosition = new Vector3(groupListOffsetX.Value, groupListOffsetY.Value, 0);
                    rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);

                    var cnt = new GameObject("GroupListStackCount");
                    cnt.transform.SetParent(gameObject.transform);
                    Text text = cnt.AddComponent<Text>();
                    text.text = count.ToString();
                    text.font = font;
                    text.color = new Color(1f, 1f, 1f, 1f);
                    text.fontSize = fs;
                    text.resizeTextForBestFit = false;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    text.alignment = TextAnchor.MiddleCenter;

                    rectTransform = text.GetComponent<RectTransform>();
                    rectTransform.localPosition = new Vector3(groupListOffsetX.Value, groupListOffsetY.Value, 0);
                    rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineRocketBackAndForthInterplanetaryExchange), "ProcessRocketLanded")]
        static bool Patch_MachineRocketBackAndForthInterplanetaryExchange_ProcessRocketLanded(
            MachineRocketBackAndForthInterplanetaryExchange __instance, Inventory ____inventory
        )
        {
            if (stackSize.Value > 1 && stackInterplanetaryRockets.Value)
            {
                if (____inventory.GetSize() * stackSize.Value <= fInventoryWorldObjectsInInventory(____inventory).Count
                    && __instance.GetComponent<SettingProxy>().GetSetting() == 1)
                {
                    CoroutineHandler.Instance.StartCoroutine(RelaunchRocket(__instance, ____inventory));
                }
                return false;
            }
            return true;
        }

        static IEnumerator RelaunchRocket(MachineRocketBackAndForthInterplanetaryExchange __instance, Inventory ____inventory)
        {
            yield return new WaitForSeconds(5f);
            if (__instance == null || ____inventory == null)
            {
                yield break;
            }
            if (____inventory.GetSize() * stackSize.Value <= fInventoryWorldObjectsInInventory(____inventory).Count
                && __instance.GetComponent<SettingProxy>().GetSetting() == 1)
            {
                __instance.SendBackAndForthRocket();
            }
        }
    }
}
