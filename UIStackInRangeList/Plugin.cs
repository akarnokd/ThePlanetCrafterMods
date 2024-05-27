// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using BepInEx.Configuration;

namespace UIStackInRangeList
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uistackinrangelist", "(UI) Stack In-Range List", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> stackPins;
        static ConfigEntry<bool> stackPortals;

        static ManualLogSource logger;

        static Color defaultBackgroundColor = new(0.25f, 0.25f, 0.25f, 0.9f);
        static Color defaultTextColor = new(1f, 1f, 1f, 1f);

        static readonly Dictionary<string, int> groupCountsCache = [];
        static readonly List<Group> groupSetCache = [];

        static Font font;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            fontSize = Config.Bind("General", "FontSize", 15, "Font size");
            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            stackPins = Config.Bind("General", "StackPins", false, "Stack the ingredients of the pinned recipes?");
            stackPortals = Config.Bind("General", "StackPortals", false, "Stack the requirements for opening a portal?");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));

            Logger.LogInfo($"Plugin patches applied!");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GroupList), nameof(GroupList.AddGroups))]
        static bool GroupList_AddGroups(GroupList __instance, 
            List<Group> _groups,
            bool _showBacklines,
            GameObject ___imageMore,
            int ___showMoreAt,
            GameObject ___grid,
            List<GroupDisplayer> ___groupsDisplayer,
            GroupInfosDisplayerBlocksSwitches _infosDisplayerGroup
        )
        {
            if (modEnabled.Value)
            {
                if (__instance.GetComponentInParent<CanvasPinedRecipes>() != null
                    && !stackPins.Value) 
                {
                    return true;
                }
                if (__instance.GetComponentInParent<UiWorldInstanceSelector>() != null 
                    && !stackPortals.Value)
                {
                    return true;
                }

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
                    Group group = groupSetCache[i];
                    if (___showMoreAt > 0 && i == ___showMoreAt)
                    {
                        ___imageMore.SetActive(true);
                        Instantiate(___imageMore, ___grid.transform).GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
                    }
                    else
                    {
                        AddGroup_Override(group, __instance, groupCountsCache[group.id], _showBacklines, ___groupsDisplayer, _infosDisplayerGroup);
                    }
                }
                return false;
            }
            return true;
        }

        static void AddGroup_Override(Group _group, GroupList __instance,
            int count, bool _showBacklines, List<GroupDisplayer> ___groupsDisplayer,
            GroupInfosDisplayerBlocksSwitches _infosDisplayerGroup)
        {
            _infosDisplayerGroup ??= new GroupInfosDisplayerBlocksSwitches();

            GameObject gameObject = Instantiate(__instance.groupDisplayerGameObject, __instance.grid.transform);
            gameObject.GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
            var gd = gameObject.GetComponent<GroupDisplayer>();
            gd.SetGroupAndUpdateDisplay(_group, false, false, false, _showBacklines);
            ___groupsDisplayer.Add(gd);
            gameObject.AddComponent<EventHoverShowGroup>().SetHoverGroupEvent(_group, _infosDisplayerGroup);

            if (count > 1)
            {
                int fs = fontSize.Value;
                var countBackground = new GameObject("GroupListStackBackground");
                countBackground.transform.SetParent(gameObject.transform);

                Image image = countBackground.AddComponent<Image>();
                image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

                var rectTransform = image.GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector3(0, 0, 0);
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
                rectTransform.localPosition = new Vector3(0, 0, 0);
                rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);
            }
        }
    }
}
