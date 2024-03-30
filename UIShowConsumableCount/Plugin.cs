// Copyright (c) 2022-2024, David Karnok & Contributors
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
    public class Plugin : BaseUnityPlugin
    {

        ConfigEntry<int> fontSize;

        static GameObject healthCount;

        static GameObject waterCount;

        static GameObject oxygenCount;

        readonly Dictionary<DataConfig.UsableType, GameObject> counts = [];

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
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
            Logger.LogInfo("Begin adding UI elements");

            healthCount = AddTextForGauge(PlayerGaugeHealth.Instance, "FoodConsumableCounter");
            waterCount = AddTextForGauge(PlayerGaugeThirst.Instance, "WaterConsumableCounter");
            oxygenCount = AddTextForGauge(PlayerGaugeOxygen.Instance, "OxygenConsumableCounter");

            counts[DataConfig.UsableType.Eatable] = healthCount;
            counts[DataConfig.UsableType.Drinkable] = waterCount;
            counts[DataConfig.UsableType.Breathable] = oxygenCount;

            Logger.LogInfo("Done adding UI elements");
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
            

            Logger.LogInfo("Gauge " + gauge.GetType() + " is at " + 
                grt.localPosition.x + ", " + grt.localPosition.y + " width " + grt.sizeDelta.x
                + " height " + grt.sizeDelta.y + " scale " + grt.localScale);

            Logger.LogInfo("Parent " + gauge.GetType() + " is at " +
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
                text.text = "x " + c;

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
        }
    }
}
