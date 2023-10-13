using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine.UI;

namespace UIShowConsumableCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowconsumablecount", "(UI) Show Consumable Counts", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        ConfigEntry<int> fontSize;

        static GameObject healthCount;

        static GameObject waterCount;

        static GameObject oxygenCount;
        
        Dictionary<DataConfig.UsableType, GameObject> counts = new Dictionary<DataConfig.UsableType, GameObject>();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size");

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
                    UpdateText(player.GetPlayerBackpack().GetInventory().GetInsideWorldObjects());
                }
            }
        }

        static Color defaultColor = new Color(1f, 1f, 1f, 1f);
        static Color defaultEmptyColor = new Color(1f, 0.5f, 0.5f, 1f);

        void Setup()
        {
            Logger.LogInfo("Begin adding UI elements");

            healthCount = AddTextForGauge(FindAnyObjectByType<PlayerGaugeHealth>(), "FoodConsumableCounter");
            waterCount = AddTextForGauge(FindAnyObjectByType<PlayerGaugeThirst>(), "WaterConsumableCounter");
            oxygenCount = AddTextForGauge(FindAnyObjectByType<PlayerGaugeOxygen>(), "OxygenConsumableCounter");

            counts[DataConfig.UsableType.Eatable] = healthCount;
            counts[DataConfig.UsableType.Drinkable] = waterCount;
            counts[DataConfig.UsableType.Breathable] = oxygenCount;

            Logger.LogInfo("Done adding UI elements");
        }

        GameObject AddTextForGauge(PlayerGauge gauge, string name)
        {
            int fs = fontSize.Value;

            Transform tr = gauge.gaugeSlider.transform;
            RectTransform grt = gauge.gaugeSlider.GetComponent<RectTransform>();

            GameObject result = new GameObject(name);
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

        void UpdateText(List<WorldObject> inventory)
        {
            Dictionary<DataConfig.UsableType, int> consumableCounts = new Dictionary<DataConfig.UsableType, int>();

            foreach (WorldObject wo in inventory)
            {
                if (wo.GetGroup() is GroupItem)
                {
                    GroupItem groupItem = (GroupItem)wo.GetGroup();
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
