// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using BepInEx.Configuration;
using BepInEx.Logging;

namespace UIShowRocketCount
{
    [BepInPlugin(modUiShowRocketCountGuid, "(UI) Show Rocket Counts", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modUiShowRocketCountGuid = "akarnokd.theplanetcraftermods.uishowrocketcount";

        static AccessTools.FieldRef<EventHoverShowGroup, Group> fEventHoverShowGroupAssociatedGroup;
        static ConfigEntry<int> fontSize;
        static ManualLogSource logger;

        static Font font;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            fEventHoverShowGroupAssociatedGroup = AccessTools.FieldRefAccess<EventHoverShowGroup, Group>("_associatedGroup");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size of the counter text on the craft screen");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");


            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnitsGenerationDisplayer), "RefreshDisplay")]
        static bool WorldUnitsGenerationDisplayer_RefreshDisplay(
            WorldUnitsGenerationDisplayer __instance,
            ref IEnumerator __result, 
            float timeRepeat,
            List<TextMeshProUGUI> ___textFields, 
            List<DataConfig.WorldUnitType> ___correspondingUnit)
        {
            __result = RefreshDisplayEnumerator(timeRepeat, __instance, ___textFields, ___correspondingUnit);
            return false;
        }

        static IEnumerator RefreshDisplayEnumerator(
            float timeRepeat,
            WorldUnitsGenerationDisplayer instance, 
            List<TextMeshProUGUI> textFields, 
            List<DataConfig.WorldUnitType> correspondingUnit)
        {
            WorldUnitsHandler worldUnitsHandler = null;
            for (; ; )
            {
                if (worldUnitsHandler == null)
                {
                    worldUnitsHandler = Managers.GetManager<WorldUnitsHandler>();
                    AccessTools.Field(typeof(WorldUnitsGenerationDisplayer), "worldUnitsHandler").SetValue(instance, worldUnitsHandler);
                }
                if (worldUnitsHandler != null && worldUnitsHandler.AreUnitsInited())
                {
                    for (int idx = 0; idx < textFields.Count; idx++)
                    {
                        WorldUnit unit = worldUnitsHandler.GetUnit(correspondingUnit[idx]);
                        if (unit != null)
                        {
                            string s = unit.GetDisplayStringForValue(unit.GetCurrentValuePersSec(), false, 0) + "/s";

                            var gid = unit.GetUnitType() switch
                            {
                                DataConfig.WorldUnitType.Oxygen => "RocketOxygen1",
                                DataConfig.WorldUnitType.Heat => "RocketHeat1",
                                DataConfig.WorldUnitType.Pressure => "RocketPressure1",
                                DataConfig.WorldUnitType.Plants => "RocketBiomass1",
                                DataConfig.WorldUnitType.Animals => "RocketAnimals1",
                                DataConfig.WorldUnitType.Insects => "RocketInsects1",
                                _ => ""
                            };

                            var gr = GroupsHandler.GetGroupViaId(gid);
                            var c = 0;
                            if (gr != null)
                            {
                                c = WorldObjectsHandler.Instance.GetObjectInWorldObjectsCount(gr.GetGroupData(), false);
                            }
                            if (c > 0)
                            {
                                s = c + " x -----    " + s;
                            }
                            textFields[idx].text = s;
                        }
                    }
                }
                yield return new WaitForSeconds(timeRepeat);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowCraft), "CreateGrid")]
        static void UiWindowCraft_CreateGrid(GridLayoutGroup ___grid)
        {
            int fs = fontSize.Value;

            foreach (Transform tr in ___grid.transform)
            {
                var ehg = tr.gameObject.GetComponent<EventHoverShowGroup>();
                if (ehg != null)
                {
                    if (fEventHoverShowGroupAssociatedGroup(ehg) is Group g)
                    {
                        if (g.id.StartsWith("Rocket") && g.id != "RocketReactor")
                        {
                            var c = WorldObjectsHandler.Instance.GetObjectInWorldObjectsCount(g.GetGroupData(), false);
                            if (c > 0)
                            {
                                var go = new GameObject();
                                Transform parent = tr.gameObject.transform;
                                go.transform.SetParent(parent, false);

                                var text = go.AddComponent<Text>();
                                text.font = font;
                                text.text = c + " x";
                                text.color = new Color(1f, 1f, 1f, 1f);
                                text.fontSize = fs;
                                text.resizeTextForBestFit = false;
                                text.verticalOverflow = VerticalWrapMode.Truncate;
                                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                                text.alignment = TextAnchor.MiddleCenter;

                                Vector2 v = tr.gameObject.GetComponent<Image>().GetComponent<RectTransform>().sizeDelta;

                                var rectTransform = text.GetComponent<RectTransform>();
                                rectTransform.localPosition = new Vector3(0, v.y / 2 + 10, 0);
                                rectTransform.sizeDelta = new Vector2(fs * 3, fs + 5);
                            }
                        }
                    }
                }
            }
        }

    }
}
