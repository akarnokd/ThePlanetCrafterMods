// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using BepInEx.Configuration;
using System.Text;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using System.Collections;

namespace UIShowAutoCrafterRecipe
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowautocrafterrecipe", "(UI) Show Auto-Crafter Recipe", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modCheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";

        // static ManualLogSource logger;

        static Plugin me;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<int> fontSize;

        static ConfigEntry<string> colorAvail;

        static ConfigEntry<string> colorMissing;

        static AccessTools.FieldRef<MachineAutoCrafter, List<(GameObject, Group)>> fMachineAutoCrafterGosInRangeForListing;

        static bool stackingRangeCheckAugment;

        static AccessTools.FieldRef<object, List<(GameObject, Group)>> fInventoryStackingGosInRangeForListing;

        static AccessTools.FieldRef<object, Dictionary<Group, List<WorldObject>>> fInventoryStackingAutocrafterWorldObjects;

        static AccessTools.FieldRef<MachineAutoCrafter, HashSet<WorldObject>> fMachineAutoCrafterWorldObjectsInRange;

        static Font font;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // logger = Logger;
            me = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            fontSize = Config.Bind("General", "FontSize", 16, "The font size of the text");
            colorAvail = Config.Bind("General", "ColorAvailable", "#FFFFFF", "The color for when an ingredient is fully available, as #RRGGBB hex.");
            colorMissing = Config.Bind("General", "ColorMissing", "#FFFF00", "The color for when an ingredient is partially or fully missing, as #RRGGBB hex.");


            fMachineAutoCrafterGosInRangeForListing = AccessTools.FieldRefAccess<List<(GameObject, Group)>>(typeof(MachineAutoCrafter), "_gosInRangeForListing");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (Chainloader.PluginInfos.TryGetValue(modCheatInventoryStackingGuid, out var pi))
            {
                Logger.LogInfo("Mod " + modCheatInventoryStackingGuid + " found, accessing its range check data.");

                fInventoryStackingGosInRangeForListing = AccessTools.FieldRefAccess<List<(GameObject, Group)>>(pi.Instance.GetType(), "_gosInRangeForListingRef");

                fInventoryStackingAutocrafterWorldObjects = AccessTools.FieldRefAccess<Dictionary<Group, List<WorldObject>>>(pi.Instance.GetType(), "autocrafterWorldObjects");

                stackingRangeCheckAugment = true;
            }
            else
            {
                Logger.LogInfo("Optional mod " + modCheatInventoryStackingGuid + " not found.");
            }

            fMachineAutoCrafterWorldObjectsInRange = AccessTools.FieldRefAccess<HashSet<WorldObject>>(typeof(MachineAutoCrafter), "_worldObjectsInRange");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }


        static void OnModConfigChanged(ConfigEntryBase _)
        {
            Destroy(parent);
        }

        static GameObject parent;
        static Text parentText;
        static RectTransform parentRectTransform;
        static RectTransform imageRectTransform;
        static Coroutine rangeCoroutine;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), "Start")]
        static void UiWindowGroupSelector_Start(UiWindowGroupSelector __instance)
        {
            if (modEnabled.Value)
            {
                __instance.StopAllCoroutines();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), nameof(UiWindowGroupSelector.OnOpenAutoCrafter))]
        static void UiWindowGroupSelector_OnOpenAutoCrafter(UiWindowGroupSelector __instance)
        {
            if (rangeCoroutine != null)
            {
                me.StopCoroutine(rangeCoroutine);
                rangeCoroutine = null;
            }
            if (modEnabled.Value)
            {
                rangeCoroutine = me.StartCoroutine(GetRangeCoroutine(__instance));
            }
        }

        static IEnumerator GetRangeCoroutine(UiWindowGroupSelector __instance)
        {
            for (; ; )
            {
                yield return new WaitForSeconds(1.5f);
                __instance.UpdateListInRange();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), nameof(UiWindowGroupSelector.UpdateListInRange))]
        static void UiWindowGroupSelector_UpdateListInRange_Pre(
            LinkedGroupsProxy ____worldObjectProxy, MachineAutoCrafter ____autoCrafter)
        {
            if (stackingRangeCheckAugment && ____autoCrafter != null && modEnabled.Value)
            {
                fInventoryStackingGosInRangeForListing() = [];

                var linkedGroups = ____worldObjectProxy.GetLinkedGroups();

                var dict = fInventoryStackingAutocrafterWorldObjects();
                dict.Clear();
                if (linkedGroups != null && linkedGroups.Count != 0)
                {
                    var recipe = linkedGroups[0].GetRecipe().GetIngredientsGroupInRecipe();
                    foreach (var group in recipe)
                    {
                        dict.TryAdd(group, new(10));
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), "UpdateGroupSelectorDisplay")]
        static void UiWindowGroupSelector_UpdateGroupSelectorDisplay(UiWindowGroupSelector __instance)
        {
            if (modEnabled.Value)
            {
                __instance.UpdateListInRange();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), nameof(UiWindowGroupSelector.UpdateListInRange))]
        static void UiWindowGroupSelector_UpdateListInRange(
            MachineAutoCrafter ____autoCrafter,
            Image ___loadingAutoCrafterImage,
            LinkedGroupsProxy ____worldObjectProxy)
        {
            if (____autoCrafter == null || !modEnabled.Value)
            {
                return;
            }
            if (parent == null)
            {
                parent = new GameObject("UIShowAutoCrafterRecipe");
                parent.transform.SetParent(___loadingAutoCrafterImage.transform.parent, false);

                parentText = parent.AddComponent<Text>();
                parentText.fontSize = fontSize.Value;
                parentText.font = font;
                parentText.alignment = TextAnchor.MiddleLeft;
                parentText.horizontalOverflow = HorizontalWrapMode.Overflow;
                parentText.verticalOverflow = VerticalWrapMode.Overflow;
                parentText.color = Color.white;
                parentText.supportRichText = true;

                parentRectTransform = parent.GetComponent<RectTransform>();

                imageRectTransform = ___loadingAutoCrafterImage.GetComponent<RectTransform>();
            }

            var inRange = fMachineAutoCrafterWorldObjectsInRange();
            if (stackingRangeCheckAugment)
            {
                inRange.Clear();
                var dict = fInventoryStackingAutocrafterWorldObjects();
                foreach (var list in dict.Values)
                {
                    foreach (var worldObject in list)
                    {
                        inRange.Add(worldObject);
                    }
                }
                dict.Clear();
                fInventoryStackingGosInRangeForListing() = null;
            }

            var inRangeCounts = new Dictionary<Group, int>();
            foreach (var objGroup in inRange)
            {
                var gr = objGroup.GetGroup();
                inRangeCounts.TryGetValue(gr, out var c);
                inRangeCounts[gr] = c + 1;
            }

            var linkedGroups = ____worldObjectProxy.GetLinkedGroups();
            var linkedGroupCounts = new Dictionary<Group, int>();
            if (linkedGroups != null && linkedGroups.Count != 0)
            {
                var recipe = linkedGroups[0].GetRecipe().GetIngredientsGroupInRecipe();
                foreach (var group in recipe)
                {
                    linkedGroupCounts.TryGetValue(group, out var c);
                    linkedGroupCounts[group] = c + 1;
                }
            }


            var sb = new StringBuilder(128);
            if (linkedGroupCounts.Count != 0)
            {
                // sb.Append("Recipe:\n");
                foreach (var group in linkedGroupCounts)
                {
                    int required = group.Value;
                    inRangeCounts.TryGetValue(group.Key, out var available);
                    available = Mathf.Min(available, required);
                    sb.Append("- <color=");
                    if (required > available)
                    {
                        sb.Append(colorMissing.Value);
                    }
                    else
                    {
                        sb.Append(colorAvail.Value);
                    }
                    sb.Append('>');
                    sb.Append(Readable.GetGroupName(group.Key));
                    sb.Append("  ( ");
                    sb.Append(available);
                    sb.Append(" / ");
                    sb.Append(required);
                    sb.Append(" )</color>");
                    sb.Append('\n');
                }

                sb.Length--;
            }

            parentText.text = sb.ToString();

            parentRectTransform.localPosition = imageRectTransform.localPosition + new Vector3(imageRectTransform.sizeDelta.x / 2 + 20 + parentText.preferredWidth / 2, 0, 0);

            parentRectTransform.sizeDelta = new Vector2(parentText.preferredWidth, parentText.preferredHeight);

            parent.SetActive(true);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), nameof(UiWindowGroupSelector.OnClose))]
        static void UiWindowGroupSelector_OnClose(UiWindowGroupSelector __instance)
        {
            if (parent != null)
            {
                parent.SetActive(false);
            }
            if (rangeCoroutine != null)
            {
                me.StopCoroutine(rangeCoroutine);
                rangeCoroutine = null;
            }
        }
    }
}
