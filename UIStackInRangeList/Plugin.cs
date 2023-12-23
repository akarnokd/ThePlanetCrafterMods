using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;
using BepInEx.Configuration;
using System;
using System.ComponentModel;

namespace UIStackInRangeList
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uistackinrangelist", "(UI) Stack In-Range List", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> stackPins;

        static ManualLogSource logger;

        static Color defaultBackgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
        static Color defaultTextColor = new Color(1f, 1f, 1f, 1f);

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            fontSize = Config.Bind("General", "FontSize", 15, "Font size");
            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            stackPins = Config.Bind("General", "StackPins", false, "Stack the ingredients of the pinned recipes?");

            Harmony.CreateAndPatchAll(typeof(Plugin));

            Logger.LogInfo($"Plugin patches applied!");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GroupList), nameof(GroupList.AddGroups))]
        static bool GroupList_AddGroups(GroupList __instance, 
            List<Group> _groups,
            GameObject ___imageMore,
            int ___showMoreAt,
            GameObject ___grid,
            bool _showBacklines,
            List<GroupDisplayer> ___groupsDisplayer
        )
        {
            if (modEnabled.Value)
            {
                if (__instance.GetComponentInParent<CanvasPinedRecipes>() != null
                    && !stackPins.Value) 
                {
                    return true;
                }
                Dictionary<string, int> groupCounts = new();
                List<Group> groupSet = new();

                foreach (var gr in _groups)
                {
                    if (!groupCounts.TryGetValue(gr.id, out var cnt))
                    {
                        groupSet.Add(gr);
                    }
                    groupCounts[gr.id] = cnt + 1;
                }

                for (int i = 0; i < groupSet.Count; i++)
                {
                    Group group = groupSet[i];
                    if (___showMoreAt > 0 && i == ___showMoreAt)
                    {
                        ___imageMore.SetActive(true);
                        Instantiate(___imageMore, ___grid.transform).GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
                    }
                    else
                    {
                        AddGroup_Override(group, __instance, groupCounts[group.id], _showBacklines, ___groupsDisplayer);
                    }
                }
                return false;
            }
            return true;
        }

        static void AddGroup_Override(Group _group, GroupList __instance, 
            int count, bool _showBacklines, List<GroupDisplayer> ___groupsDisplayer)
        {
            var infoDG = new GroupInfosDisplayerBlocksSwitches();

            GameObject gameObject = Instantiate(__instance.groupDisplayerGameObject, __instance.grid.transform);
            gameObject.GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
            var gd = gameObject.GetComponent<GroupDisplayer>();
            gd.SetGroupAndUpdateDisplay(_group, false, false, false, _showBacklines);
            ___groupsDisplayer.Add(gd);
            gameObject.AddComponent<EventHoverShowGroup>().SetHoverGroupEvent(_group, infoDG);

            if (count > 1)
            {
                int fs = fontSize.Value;
                GameObject countBackground = new GameObject("GroupListStackBackground");
                countBackground.transform.SetParent(gameObject.transform);

                Image image = countBackground.AddComponent<Image>();
                image.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);

                var rectTransform = image.GetComponent<RectTransform>();
                rectTransform.localPosition = new Vector3(0, 0, 0);
                rectTransform.sizeDelta = new Vector2(2 * fs, fs + 5);

                GameObject cnt = new GameObject("GroupListStackCount");
                cnt.transform.SetParent(gameObject.transform);
                Text text = cnt.AddComponent<Text>();
                text.text = count.ToString();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
