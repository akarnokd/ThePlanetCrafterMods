// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine.UI;
using System.Collections.ObjectModel;

namespace UIShowConsumableCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowconsumablecount", "(UI) Show Consumable Counts", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        ConfigEntry<int> fontSize;

        ConfigEntry<bool> debugMode;

        static GameObject healthCount;

        static GameObject waterCount;

        static GameObject oxygenCount;

        static GameObject purifyCount;

        readonly Dictionary<DataConfig.UsableType, GameObject> counts = [];

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debug logging (chatty!)");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void LogInfo(string message)
        {
            if (debugMode.Value)
            {
                Logger.LogInfo(message);
            }
        }

        void Update()
        {
            PlayersManager playersManager = Managers.GetManager<PlayersManager>();
            if (playersManager != null)
            {
                PlayerMainController player = playersManager.GetActivePlayerController();
                if (player != null)
                {
                    if (healthCount == null)
                    {
                        Setup();
                    }
                    var items = player.GetPlayerBackpack()?.GetInventory()?.GetInsideWorldObjects();
                    if (items != null)
                    {
                        UpdateText(items);
                    }
                }
            }
        }

        static Color defaultColor = new(1f, 1f, 1f, 1f);
        static Color defaultEmptyColor = new(1f, 0.5f, 0.5f, 1f);

        void Setup()
        {
            LogInfo("Begin adding UI elements");

            healthCount = AddTextForGauge(PlayerGaugeHealth.Instance, "FoodConsumableCounter");
            waterCount = AddTextForGauge(PlayerGaugeThirst.Instance, "WaterConsumableCounter");
            oxygenCount = AddTextForGauge(PlayerGaugeOxygen.Instance, "OxygenConsumableCounter");
            purifyCount = AddTextForGauge(PlayerGaugeToxic.Instance, "PurifyConsumableCounter");

            counts[DataConfig.UsableType.Eatable] = healthCount;
            counts[DataConfig.UsableType.Drinkable] = waterCount;
            counts[DataConfig.UsableType.Breathable] = oxygenCount;
            counts[DataConfig.UsableType.Purify] = purifyCount;

            LogInfo("Done adding UI elements");
        }

        GameObject AddTextForGauge<T>(PlayerGauge<T> gauge, string name) where T : PlayerGauge<T>
        {
            int fs = fontSize.Value;

            Transform tr = gauge.gaugeSlider.transform;
            RectTransform grt = gauge.gaugeSlider.GetComponent<RectTransform>();

            var result = new GameObject(name);
            result.transform.parent = tr;

            Text text = result.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = "xxxx";
            text.color = defaultColor;
            text.fontSize = fs;
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.alignment = TextAnchor.UpperLeft;

            float x = grt.sizeDelta.x * grt.localScale.x + 10;
            float y = 0;

            RectTransform rect = text.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(x, y, 0);
            rect.sizeDelta = new Vector2(fs * 5, fs + 5);
            

            LogInfo("Gauge " + gauge.GetType() + " is at " + 
                grt.localPosition.x + ", " + grt.localPosition.y + " width " + grt.sizeDelta.x
                + " height " + grt.sizeDelta.y + " scale " + grt.localScale);

            LogInfo("Parent " + gauge.GetType() + " is at " +
                tr.localPosition.x + ", " + tr.localPosition.y + " scale " + tr.localScale);

            return result;
        }

        void UpdateText(ReadOnlyCollection<WorldObject> inventory)
        {
            Dictionary<DataConfig.UsableType, int> consumableCounts = [];

            foreach (WorldObject wo in inventory)
            {
                if (wo.GetGroup() is GroupItem groupItem)
                {
                    DataConfig.UsableType key = groupItem.GetUsableType();
                    consumableCounts.TryGetValue(key, out int c);
                    consumableCounts[key] = c + 1;
                }
            }

            foreach (KeyValuePair<DataConfig.UsableType, GameObject> pair in counts)
            {
                consumableCounts.TryGetValue(pair.Key, out int c);

                Text text = pair.Value.GetComponent<Text>();
                text.text = string.Format(Localization.GetLocalizedString("ShowConsumableCount_Amount"), c);

                if (c > 0)
                {
                    text.color = defaultColor;
                }
                else
                {
                    text.color = defaultEmptyColor;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            bool active = ___uisToHide[0].activeSelf;
            healthCount?.SetActive(active);
            waterCount?.SetActive(active);
            oxygenCount?.SetActive(active);
            purifyCount?.SetActive(active);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
            Dictionary<string, Dictionary<string, string>> ___localizationDictionary
        )
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["ShowConsumableCount_Amount"] = "x {0}";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["ShowConsumableCount_Amount"] = "x {0}";
            }
            if (___localizationDictionary.TryGetValue("russian", out dict))
            {
                dict["ShowConsumableCount_Amount"] = "{0} шт.";
            }
        }

    }
}
