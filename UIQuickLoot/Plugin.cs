// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.UIElements.UIR;

namespace UIQuickLoot
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiquickloot", "(UI) Quick Loot", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";

        static Plugin me;

        static ManualLogSource logger;

        /// <summary>
        /// If the CheatInventoryStacking is installed, consider the stack counts when displaying information.
        /// </summary>
        static Func<IEnumerable<WorldObject>, int> getStackCount;

        static ConfigEntry<int> stackSizeConfig;
        static Func<int, bool> apiCanStack;

        static Actionnable currentActionable;

        static GameObject panel;
        static GameObject panelGo;
        static Image panelBackground;
        static RectTransform panelRt;

        static GameObject emptyGo;
        static Text emptyText;
        
        static GameObject scrollUp;
        static Text scrollUpText;
        static RectTransform scrollUpRt;
        static Image scrollUpBackground;
        static RectTransform scrollUpBackgroundRt;

        static GameObject scrollDown;
        static Text scrollDownText;
        static RectTransform scrollDownRt;
        static Image scrollDownBackground;
        static RectTransform scrollDownBackgroundRt;

        static GameObject shortcutTip;
        static RectTransform shortcutTipRt;
        static Text shortcutTipText;
        static Image shortcutTipBackground;
        static RectTransform shortcutTipBackgroundRt;

        static readonly List<PanelEntry> panelEntries = [];

        static int selectedIndex;
        static int scrollTop;
        static Inventory currentInventory;
        static readonly Dictionary<string, int> groupCounts = [];
        static readonly Dictionary<string, WorldObject> groupsWorldObjects = [];
        static readonly List<string> groupsFound = [];

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> debugMode;

        static ConfigEntry<int> panelX;
        static ConfigEntry<int> panelY;
        static ConfigEntry<int> rowCount;
        static ConfigEntry<int> panelWidth;
        static ConfigEntry<float> panelOpacity;
        static ConfigEntry<int> rowHeight;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<int> margin;
        static ConfigEntry<int> amountWidth;
        static ConfigEntry<bool> showShortcuts;

        static ConfigEntry<string> keyTakeOne;
        static ConfigEntry<string> keyTakeAll;

        static Font font;

        static Color normalRowColor = new(0.1f, 0.1f, 0.1f, 0);
        static Color selectedRowColor = new(0.5f, 0f, 5.0f, 0.95f);

        static InputAction takeOne;
        static InputAction takeAll;

        static ConfigEntry<bool> allowPlayerContainers;
        static ConfigEntry<bool> allowWorldContainers;
        static ConfigEntry<bool> allowWreckContainers;
        static ConfigEntry<bool> allowAutoCrafters;
        static ConfigEntry<bool> allowRecyclerIns;
        static ConfigEntry<bool> allowRecyclerOuts;
        static ConfigEntry<bool> allowOreExtractors;
        static ConfigEntry<bool> allowOreCrusherIns;
        static ConfigEntry<bool> allowOreCrusherOuts;
        static ConfigEntry<bool> allowWaterCollectors;
        static ConfigEntry<bool> allowFoodGrowers;
        static ConfigEntry<bool> allowFarms;
        static ConfigEntry<bool> allowGasExtractors;
        static ConfigEntry<bool> allowBeehives;
        static ConfigEntry<bool> allowButterflyFarms;
        static ConfigEntry<bool> allowFishFarms;
        static ConfigEntry<bool> allowFrogFarms;
        static ConfigEntry<bool> allowEcosystems;
        static ConfigEntry<bool> allowFlowerSpreaders;
        static ConfigEntry<bool> allowTreeSpreaders;
        static ConfigEntry<bool> allowBiodomes;
        static ConfigEntry<bool> allowGeneticExtractors;
        static ConfigEntry<bool> allowSequencers;
        static ConfigEntry<bool> allowIncubators;
        static ConfigEntry<bool> allowSynthetizers;
        static ConfigEntry<bool> allowRoverStorages;
        static ConfigEntry<bool> allowRoverEquipments;
        static ConfigEntry<bool> allowAnimalFeeders;
        static ConfigEntry<bool> allowOptimizers;
        static ConfigEntry<bool> allowDetoxifyIns;
        static ConfigEntry<bool> allowDetoxifyOuts;
        static ConfigEntry<bool> allowDefault;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            me = this;
            logger = Logger;

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging (chatty!)?");

            keyTakeOne = Config.Bind("Keys", "TakeOne", "<Keyboard>/E", "Key to press to take one item.");
            keyTakeAll = Config.Bind("Keys", "TakeAll", "<Keyboard>/R", "Key to press to take all items.");

            allowPlayerContainers = Config.Bind("Settings", "AllowPlayerContainers", true, "Allow quick looting on Player containers?");
            allowWorldContainers = Config.Bind("Settings", "AllowWorldContainers", true, "Allow quick looting on world containers?");
            allowWreckContainers = Config.Bind("Settings", "AllowWreckContainers", true, "Allow quick looting on wreck containers?");
            allowAutoCrafters = Config.Bind("Settings", "AllowAutoCrafters", true, "Allow quick looting on Auto-Crafters?");
            allowRecyclerIns = Config.Bind("Settings", "AllowRecyclerIns", false, "Allow quick looting on Recycler inputs?");
            allowRecyclerOuts = Config.Bind("Settings", "AllowRecyclerOuts", true, "Allow quick looting on Recycler outputs?");
            allowOreExtractors = Config.Bind("Settings", "AllowOreExtractors", true, "Allow quick looting on Ore extractors?");
            allowOreCrusherIns = Config.Bind("Settings", "AllowOreCrusherIns", false, "Allow quick looting on Ore crusher inputs?");
            allowOreCrusherOuts = Config.Bind("Settings", "AllowOreCrusherOuts", true, "Allow quick looting on Ore crusher outputs?");
            allowWaterCollectors = Config.Bind("Settings", "AllowWaterCollectors", true, "Allow quick looting on Water collectors?");
            allowFoodGrowers = Config.Bind("Settings", "AllowFoodGrowers", false, "Allow quick looting on Food growers?");
            allowFarms = Config.Bind("Settings", "AllowFarms", false, "Allow quick looting on Farms?");
            allowGasExtractors = Config.Bind("Settings", "AllowGasExtractors", true, "Allow quick looting on Gas extractors?");
            allowBeehives = Config.Bind("Settings", "AllowBeehives", true, "Allow quick looting on Beehives?");
            allowButterflyFarms = Config.Bind("Settings", "AllowButterflyFarms", false, "Allow quick looting on Butterfly farms?");
            allowFishFarms = Config.Bind("Settings", "AllowFishflyFarms", false, "Allow quick looting on Fish farms?");
            allowFrogFarms = Config.Bind("Settings", "AllowFrogFarms", false, "Allow quick looting on Frog farms?");
            allowEcosystems = Config.Bind("Settings", "AllowEcosystems", true, "Allow quick looting on Ecosystems?");
            allowFlowerSpreaders = Config.Bind("Settings", "AllowFlowerSpreaders", false, "Allow quick looting on Flower spreaders?");
            allowTreeSpreaders = Config.Bind("Settings", "AllowTreeSpreaders", false, "Allow quick looting on Tree spreaders?");
            allowBiodomes = Config.Bind("Settings", "AllowBiodomes", true, "Allow quick looting on Biodomes?");
            allowGeneticExtractors = Config.Bind("Settings", "AllowGeneticExtractors", false, "Allow quick looting on Genetic extractors?");
            allowSequencers = Config.Bind("Settings", "AllowSequencers", true, "Allow quick looting on DNA sequencers?");
            allowIncubators = Config.Bind("Settings", "AllowIncubators", true, "Allow quick looting on Incubators?");
            allowSynthetizers = Config.Bind("Settings", "AllowSynthetizers", false, "Allow quick looting on Synthetizers?");
            allowRoverStorages = Config.Bind("Settings", "AllowRoverStorages", true, "Allow quick looting on Rover storages?");
            allowRoverEquipments = Config.Bind("Settings", "AllowRoverEquipments", false, "Allow quick looting on Rover equipments?");
            allowAnimalFeeders = Config.Bind("Settings", "AllowAnimalFeeders", false, "Allow quick looting on Animal feeders?");
            allowOptimizers = Config.Bind("Settings", "AllowOptimizers", false, "Allow quick looting on Optimizers?");
            allowDefault = Config.Bind("Settings", "AllowDefault", true, "When none of the other filters apply, what should be the default logic?");
            allowDetoxifyIns = Config.Bind("Settings", "AllowDetoxifyIns", false, "Allow quick looting on Detoxify Machine inputs?");
            allowDetoxifyOuts = Config.Bind("Settings", "AllowDetoxifyOuts", true, "Allow quick looting on Detoxify Machine outputs?");


            panelX = Config.Bind("UI", "PanelX", 100, "Shift the panel in the X direction by this amount relative to screen center.");
            panelY = Config.Bind("UI", "PanelY", 0, "Shift the panel in the Y direction by this amount relative to screen center.");
            panelWidth = Config.Bind("UI", "PanelWidth", 450, "The width of the panel.");
            panelOpacity = Config.Bind("UI", "PanelOpacity", 0.99f, "The opacity: 1 - fully opaque, 0 fully transparent.");
            rowCount = Config.Bind("UI", "RowCount", 9, "The number of rows to display at once.");
            rowHeight = Config.Bind("UI", "RowHeight", 32, "The height of rows in pixels.");
            fontSize = Config.Bind("UI", "FontSize", 24, "The font size");
            margin = Config.Bind("UI", "Margin", 5, "The margin between visual elements.");
            amountWidth = Config.Bind("UI", "AmountWidth", 100, "The width of the amount field.");
            showShortcuts = Config.Bind("UI", "ShowShortcuts", true, "Show the shortcuts tips panel?");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (Chainloader.PluginInfos.TryGetValue(modInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                Logger.LogInfo("Mod " + modInventoryStackingGuid + " found, considering stacking in various inventories");

                Type modType = pi.Instance.GetType();

                getStackCount = (Func<IEnumerable<WorldObject>, int>)AccessTools.Field(modType, "apiGetStackCount").GetValue(null);
                stackSizeConfig = (ConfigEntry<int>)AccessTools.Field(modType, "stackSize").GetValue(null);
                apiCanStack = (Func<int, bool>)AccessTools.Field(modType, "apiCanStack").GetValue(null);
            }
            else
            {
                getStackCount = wos => wos.Count();
                apiCanStack = inv => false;
                stackSizeConfig = null;
                Logger.LogInfo("Mod " + modInventoryStackingGuid + " not found");
            }

            UpdateKeyBindings();

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void Log(string message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnHover))]
        static void ActionOpenable_OnHover(
            ActionOpenable __instance)
        {
            if (!modEnabled.Value)
            {
                return;
            }
            var inventoryAssoc = __instance.GetComponent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance));
                return;
            }

            var inventoryAssocProxy = __instance.GetComponent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance));
                return;
            }

            inventoryAssoc = __instance.GetComponentInParent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance));
                return;
            }

            inventoryAssocProxy = __instance.GetComponentInParent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance));
                return;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnHoverOut))]
        static void ActionOpenable_OnHoverOut(ActionOpenable __instance)
        {
            if (__instance == currentActionable)
            {
                currentActionable = null;
                if (currentInventory != null)
                {
                    currentInventory.inventoryContentModified -= OnInventoyModified;
                }
                currentInventory = null;
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionGroupSelector), nameof(ActionGroupSelector.OnHover))]
        static void ActionGroupSelector_OnHover(
            ActionOpenable __instance)
        {
            if (!modEnabled.Value)
            {
                return;
            }
            var inventoryAssoc = __instance.GetComponent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance));
                return;
            }

            var inventoryAssocProxy = __instance.GetComponent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance));
                return;
            }

            inventoryAssoc = __instance.GetComponentInParent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance));
                return;
            }

            inventoryAssocProxy = __instance.GetComponentInParent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance));
                return;
            }

            return;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ActionGroupSelector), nameof(ActionGroupSelector.OnHoverOut))]
        static void ActionGroupSelector_OnHoverOut(ActionGroupSelector __instance)
        {
            if (__instance == currentActionable)
            {
                currentActionable = null;
                if (currentInventory != null)
                {
                    currentInventory.inventoryContentModified -= OnInventoyModified;
                }
                currentInventory = null;
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }

        static bool CheckContainerType(Actionnable __instance)
        {
            var sb = new StringBuilder(256);
            var go = __instance.gameObject;
            while (go != null)
            {
                sb.Append(go.name).Append('/');
                if (go.transform.parent != null)
                {
                    go = go.transform.parent.gameObject;
                }
                else
                {
                    break;
                }
            }
            var path = sb.ToString();
            Log("CheckContainerType: " + path);

            if (path.Contains("Container1", StringComparison.InvariantCultureIgnoreCase)
                || path.Contains("Container2", StringComparison.InvariantCultureIgnoreCase)
                || path.Contains("Container3", StringComparison.InvariantCultureIgnoreCase)
                )
            {
                return allowPlayerContainers.Value;
            }
            if (path.Contains("World", StringComparison.InvariantCultureIgnoreCase)
                || path.Contains("Golden", StringComparison.InvariantCultureIgnoreCase)
                || path.Contains("Vault", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowWorldContainers.Value;
            }
            if (path.Contains("Wreck", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowWreckContainers.Value;
            }
            if (path.Contains("AutoCrafter", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowAutoCrafters.Value;
            }
            if (path.Contains("Recycling", StringComparison.InvariantCultureIgnoreCase))
            {
                if (path.Contains("ContainerLeft", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowRecyclerIns.Value;
                }
                else
                if (path.Contains("ContainerRight", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowRecyclerOuts.Value;
                }
                return allowRecyclerIns.Value;
            }
            if (path.Contains("OreBreaker", StringComparison.InvariantCultureIgnoreCase))
            {
                if (path.Contains("ContainerLeft", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowOreCrusherIns.Value;
                }
                else
                if (path.Contains("ContainerRight", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowOreCrusherOuts.Value;
                }
            }
            if (path.Contains("DetoxificationMachine", StringComparison.InvariantCultureIgnoreCase))
            {
                if (path.Contains("ContainerLeft", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowDetoxifyIns.Value;
                }
                else
                if (path.Contains("ContainerRight", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowDetoxifyOuts.Value;
                }
            }
            if (path.Contains("OreExtractor", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowOreExtractors.Value;
            }
            if (path.Contains("WaterCollector", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowWaterCollectors.Value;
            }
            if (path.Contains("FoodGrower", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowFoodGrowers.Value;
            }
            if (path.Contains("Farm", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowFarms.Value;
            }
            if (path.Contains("GasExtractor", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowGasExtractors.Value;
            }
            if (path.Contains("Beehive", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowBeehives.Value;
            }
            if (path.Contains("Butterfly", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowButterflyFarms.Value;
            }
            if (path.Contains("Fish", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowFishFarms.Value;
            }
            if (path.Contains("Frog", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowFrogFarms.Value;
            }
            if (path.Contains("Ecosystem", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowEcosystems.Value;
            }
            if (path.Contains("SeedSpreader", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowFlowerSpreaders.Value;
            }
            if (path.Contains("TreesSpreader", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowTreeSpreaders.Value;
            }
            if (path.Contains("Biodome", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowBiodomes.Value;
            }
            if (path.Contains("GeneticExtractor", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowGeneticExtractors.Value;
            }
            if (path.Contains("Sequencer", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowSequencers.Value;
            }
            if (path.Contains("Incubator", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowIncubators.Value;
            }
            if (path.Contains("VehicleTruck", StringComparison.InvariantCultureIgnoreCase))
            {
                if (path.Contains("InventoryStorage", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowRoverStorages.Value;
                }
                if (path.Contains("InventoryEquipment", StringComparison.InvariantCultureIgnoreCase))
                {
                    return allowRoverEquipments.Value;
                }
            }
            if (path.Contains("GeneticSynthetizer", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowSynthetizers.Value;
            }
            if (path.Contains("AnimalFeeder", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowAnimalFeeders.Value;
            }
            if (path.Contains("Optimizer", StringComparison.InvariantCultureIgnoreCase))
            {
                return allowOptimizers.Value;
            }

            return allowDefault.Value;
        }

        static readonly Action<WorldObject, bool> OnInventoyModified = (wo, add) => UpdateDisplay();

        static void OnInventory(Inventory inventory, Actionnable __instance)
        {
            if (currentInventory != null)
            {
                currentInventory.inventoryContentModified -= OnInventoyModified;
            }
            if (!CheckContainerType(__instance))
            {
                return;
            }
            currentActionable = __instance;
            currentInventory = inventory;
            inventory.inventoryContentModified += OnInventoyModified;

            bool create = false;
            if (panel == null)
            {
                panel = new GameObject("UIQuickLootPanel");
                var canvas = panel.AddComponent<Canvas>();
                canvas.sortingOrder = 200;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                panelGo = new GameObject("Background");
                panelGo.transform.SetParent(panel.transform, false);
                panelBackground = panelGo.AddComponent<Image>();
                panelRt = panelGo.GetComponent<RectTransform>();

                var outline = panelGo.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(1, -1);

                panelEntries.Clear();
                create = true;
            }

            while (panelEntries.Count < rowCount.Value)
            {
                var pa = new PanelEntry();
                pa.rowObject = new GameObject("LootRow");
                pa.rowObject.transform.SetParent(panelGo.transform, false);

                pa.rowBackground = pa.rowObject.AddComponent<Image>();
                pa.rowBackground.color = Color.black;
                pa.rowRt = pa.rowObject.GetComponent<RectTransform>();

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(pa.rowObject.transform, false);
                pa.icon = iconGo.AddComponent<Image>();
                pa.iconRt = iconGo.GetComponent<RectTransform>();
                pa.icon.enabled = false;

                var countGo = new GameObject("Count");
                countGo.transform.SetParent(pa.rowObject.transform, false);
                pa.amount = countGo.AddComponent<Text>();
                pa.amount.color = Color.white;
                pa.amount.font = font;
                pa.amount.horizontalOverflow = HorizontalWrapMode.Overflow;
                pa.amount.verticalOverflow = VerticalWrapMode.Overflow;
                pa.amount.alignment = TextAnchor.MiddleRight;
                pa.amount.text = "0 x";

                pa.amountRt = countGo.GetComponent<RectTransform>();

                var nameGo = new GameObject("Name");
                nameGo.transform.SetParent(pa.rowObject.transform, false);
                pa.name = nameGo.AddComponent<Text>();
                pa.name.color = Color.white;
                pa.name.font = font;
                pa.name.horizontalOverflow = HorizontalWrapMode.Overflow;
                pa.name.verticalOverflow = VerticalWrapMode.Overflow;
                pa.name.alignment = TextAnchor.MiddleLeft;
                pa.name.text = "...";

                pa.nameRt = nameGo.GetComponent<RectTransform>();

                panelEntries.Add(pa);
            }

            while (panelEntries.Count > rowCount.Value)
            {
                var e = panelEntries[^1];
                panelEntries.RemoveAt(panelEntries.Count - 1);
                Destroy(e.rowObject);
            }

            if (create)
            {
                emptyGo = new GameObject("Empty");
                emptyGo.transform.SetParent(panelGo.transform, false);
                emptyText = emptyGo.AddComponent <Text>();
                emptyText.color = Color.white;
                emptyText.font = font;
                emptyText.horizontalOverflow = HorizontalWrapMode.Overflow;
                emptyText.verticalOverflow = VerticalWrapMode.Overflow;
                emptyText.alignment = TextAnchor.MiddleCenter;

                var scrollUpBackgroundGo = new GameObject("ScrollUpBackground");
                scrollUpBackgroundGo.transform.SetParent(panelGo.transform, false);
                scrollUpBackground = scrollUpBackgroundGo.AddComponent<Image>();
                scrollUpBackgroundRt = scrollUpBackgroundGo.GetComponent<RectTransform>();
                var outline = scrollUpBackgroundGo.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(1, -1);

                scrollUp = new GameObject("ScrollUp");
                scrollUp.transform.SetParent(panelGo.transform, false);
                scrollUpText = scrollUp.AddComponent<Text>();
                scrollUpText.color = Color.white;
                scrollUpText.font = font;
                scrollUpText.horizontalOverflow = HorizontalWrapMode.Overflow;
                scrollUpText.verticalOverflow = VerticalWrapMode.Overflow;
                scrollUpText.alignment = TextAnchor.MiddleCenter;
                scrollUpText.text = " ^^^^^^^^^^ ";
                scrollUpRt = scrollUp.GetComponent<RectTransform>();

                var scrollDownBackgroundGo = new GameObject("ScrollDownBackground");
                scrollDownBackgroundGo.transform.SetParent(panelGo.transform, false);
                scrollDownBackground = scrollDownBackgroundGo.AddComponent<Image>();
                scrollDownBackgroundRt = scrollDownBackgroundGo.GetComponent<RectTransform>();
                outline = scrollDownBackgroundGo.AddComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(1, -1);

                scrollDown = new GameObject("ScrollDown");
                scrollDown.transform.SetParent(panelGo.transform, false);
                scrollDownText = scrollDown.AddComponent<Text>();
                scrollDownText.color = Color.white;
                scrollDownText.font = font;
                scrollDownText.horizontalOverflow = HorizontalWrapMode.Overflow;
                scrollDownText.verticalOverflow = VerticalWrapMode.Overflow;
                scrollDownText.alignment = TextAnchor.MiddleCenter;
                scrollDownText.text = " vvvvvvvvvv ";
                scrollDownRt = scrollDown.GetComponent<RectTransform>();

                var shortcutTipBackgroundGo = new GameObject("ShortcutTipBackground");
                shortcutTipBackgroundGo.transform.SetParent(panelGo.transform, false);
                shortcutTipBackground = shortcutTipBackgroundGo.AddComponent<Image>();
                shortcutTipBackgroundRt = shortcutTipBackgroundGo.GetComponent<RectTransform>();

                shortcutTip = new GameObject("ShortcutTip");
                shortcutTip.transform.SetParent(panelGo.transform, false);
                shortcutTipText = shortcutTip.AddComponent<Text>();
                shortcutTipText.color = Color.white;
                shortcutTipText.font = font;
                shortcutTipText.horizontalOverflow = HorizontalWrapMode.Overflow;
                shortcutTipText.verticalOverflow = VerticalWrapMode.Overflow;
                shortcutTipText.alignment = TextAnchor.MiddleCenter;
                shortcutTipText.text = "";
                shortcutTipRt = shortcutTip.GetComponent<RectTransform>();
                /*
                outline = shortcutTip.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, -1);
                */
            }

            selectedIndex = 0;
            scrollTop = 0;

            UpdateDisplay();

            panel.SetActive(true);
        }

        static void UpdateDisplay()
        {
            if (panel == null)
            {
                return;
            }

            panelBackground.color = new Color(0, 0, 0, panelOpacity.Value);
            panelRt.localPosition = new Vector3(panelX.Value + panelWidth.Value / 2, panelY.Value, 0);
            panelRt.sizeDelta = new Vector2(panelWidth.Value, (rowCount.Value * rowHeight.Value + 2 * margin.Value));

            groupsFound.Clear();
            groupCounts.Clear();
            groupsWorldObjects.Clear();

            foreach (var wo in currentInventory.GetInsideWorldObjects())
            {
                var grid = GeneticsGrouping.GetStackId(wo);
                if (!groupCounts.TryGetValue(grid, out var cnt))
                {
                    groupsWorldObjects[grid] = wo;
                    groupsFound.Add(grid);
                }
                groupCounts[grid] = cnt + 1;
            }

            var rh = rowHeight.Value;
            int i = 0;
            int yMax = panelEntries.Count * rh / 2 - rh / 2;

            if (selectedIndex >= groupsFound.Count)
            {
                selectedIndex = groupsFound.Count - 1;
            }
            if (selectedIndex < scrollTop)
            {
                scrollTop = Math.Max(0, selectedIndex);
            }
            if (selectedIndex >= scrollTop + rowCount.Value)
            {
                scrollTop = selectedIndex - rowCount.Value;
            }

            int start = Math.Max(0, Math.Min(scrollTop, groupsFound.Count - rowCount.Value));
            int end = Math.Min(start + rowCount.Value, groupsFound.Count);
            int idx = start;

            foreach (var e in panelEntries)
            {
                if (idx < end)
                {
                    var grid = groupsFound[idx];

                    groupCounts.TryGetValue(grid, out var cnt);
                    groupsWorldObjects.TryGetValue(grid, out var wo);

                    e.icon.sprite = wo.GetGroup().GetGroupData().icon;
                    e.amount.text = cnt + " x";
                    var gr = wo.GetGroup();
                    var lg = wo.GetLinkedGroups() ?? [];
                    if (gr.id == "BlueprintT1" && lg.Count != 0)
                    {
                        var str = Localization.GetLocalizedString(GameConfig.localizationGroupNameId + lg[0].id);
                        if (string.IsNullOrEmpty(str))
                        {
                            e.name.text = lg[0].id;
                        }
                        else
                        {
                            e.name.text = str;
                        }
                    }
                    else
                    {
                        e.name.text = Readable.GetGroupName(gr);
                    }
                    e.icon.enabled = true;
                }
                else
                {
                    e.icon.sprite = null;
                    e.icon.enabled = false;
                    e.amount.text = "";
                    e.name.text = "";
                }

                if (idx == selectedIndex)
                {
                    e.rowBackground.color = selectedRowColor;
                }
                else
                {
                    e.rowBackground.color = normalRowColor;
                }

                idx++;

                e.amount.fontSize = fontSize.Value;
                e.name.fontSize = fontSize.Value;

                var pws = panelWidth.Value - 2 * margin.Value;
                e.rowRt.localPosition = new Vector3(0, yMax - i * rh, 0);
                e.rowRt.sizeDelta = new Vector2(pws, rh);

                e.iconRt.localPosition = new Vector3(-pws / 2 + rh / 2, 0, 0);
                e.iconRt.sizeDelta = new Vector3(rh, rh);

                e.amountRt.localPosition = new Vector3(-pws / 2 + rh + margin.Value, 0, 0);
                e.amountRt.sizeDelta = new Vector2(amountWidth.Value, rh);

                var nw = panelWidth.Value - rh - 2 * margin.Value - amountWidth.Value;
                e.nameRt.localPosition = new Vector3(-pws / 2 + rh + 2 * margin.Value + amountWidth.Value / 2 + nw / 2, 0, 0);
                e.nameRt.sizeDelta = new Vector2(nw, rh);

                i++;
            }

            scrollUpText.fontSize = fontSize.Value;
            scrollUpRt.localPosition = new Vector3(0, panelRt.sizeDelta.y / 2 + scrollUpText.preferredHeight / 2, 0);
            scrollUpBackground.color = panelBackground.color;
            scrollUpBackgroundRt.localPosition = scrollUpRt.localPosition;
            scrollUpBackgroundRt.sizeDelta = new Vector2(scrollUpText.preferredWidth, scrollUpText.preferredHeight);

            scrollDownText.fontSize = fontSize.Value;
            scrollDownRt.localPosition = new Vector3(0, -panelRt.sizeDelta.y / 2 - scrollDownText.preferredHeight / 2, 0);
            scrollDownBackground.color = panelBackground.color;
            scrollDownBackgroundRt.localPosition = scrollDownRt.localPosition;
            scrollDownBackgroundRt.sizeDelta = new Vector2(scrollDownText.preferredWidth, scrollDownText.preferredHeight);

            scrollUpText.gameObject.SetActive(scrollTop > 0);
            scrollUpBackground.gameObject.SetActive(scrollUpText.gameObject.activeSelf);
            scrollDownText.gameObject.SetActive(scrollTop + rowCount.Value < groupsFound.Count);
            scrollDownBackground.gameObject.SetActive(scrollDownText.gameObject.activeSelf);

            var sb = new StringBuilder();
            sb.Append("[ ")
                .Append(keyTakeOne.Value.Replace("<Keyboard>/", ""))
                .Append(" ] - ")
                .Append(Localization.GetLocalizedString("QuickLoot_TakeOne"));
            sb.Append("         [ ")
                .Append(keyTakeAll.Value.Replace("<Keyboard>/", ""))
                .Append(" ] - ")
                .Append(Localization.GetLocalizedString("QuickLoot_TakeAll"))
                .AppendLine();

            sb.Append("[ Ctrl + ")
            .Append(keyTakeOne.Value.Replace("<Keyboard>/", ""))
            .Append(" ] - ")
            .Append(Localization.GetLocalizedString("QuickLoot_TakeRow"))
            ;

            if (stackSizeConfig != null && stackSizeConfig.Value > 1 && apiCanStack(currentInventory.GetId()))
            {
                sb
                .AppendLine()
                .Append("[ Shift + ")
                .Append(keyTakeOne.Value.Replace("<Keyboard>/", ""))
                .Append(" ] - ")
                .Append(Localization.GetLocalizedString("QuickLoot_TakeStack"))
                .Append(" (")
                .Append(stackSizeConfig.Value)
                .Append(")");
            }

            shortcutTipText.fontSize = fontSize.Value * 3 / 4;
            shortcutTipText.text = sb.ToString();
            shortcutTipRt.sizeDelta = new Vector2(shortcutTipText.preferredWidth + margin.Value, shortcutTipText.preferredHeight + margin.Value);
            shortcutTipRt.localPosition = scrollDownRt.localPosition + new Vector3(0, -shortcutTipText.preferredHeight / 2 - 2 * margin.Value - fontSize.Value / 2, 0);

            shortcutTipBackground.color = panelBackground.color;
            shortcutTipBackgroundRt.localPosition = shortcutTipRt.localPosition;
            shortcutTipBackgroundRt.sizeDelta = shortcutTipRt.sizeDelta + new Vector2(margin.Value, margin.Value);

            shortcutTip.SetActive(showShortcuts.Value);
            shortcutTipBackground.gameObject.SetActive(showShortcuts.Value);

            emptyText.fontSize = fontSize.Value;
            emptyText.text = Localization.GetLocalizedString("QuickLoot_Empty");
            emptyGo.SetActive(groupsFound.Count == 0);
        }

        void Update()
        {
            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }
            var pc = pm.GetActivePlayerController();
            if (pc == null)
            {
                return;
            }
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }
            if (panel == null || !panel.activeSelf)
            {
                return;
            }
            if (wh.GetHasUiOpen())
            {
                panel.SetActive(false);
                return;
            }
            if (Mouse.current != null)
            {
                if (Mouse.current.scroll.value.y < 0)
                {
                    selectedIndex = Math.Min(groupsFound.Count - 1, selectedIndex + 1);
                    if (selectedIndex >= scrollTop + rowCount.Value)
                    {
                        scrollTop++;
                    }
                    UpdateDisplay();
                }
                else
                if (Mouse.current.scroll.value.y > 0)
                {
                    selectedIndex = Math.Max(0, selectedIndex - 1);
                    if (selectedIndex < scrollTop)
                    {
                        scrollTop--;
                    }
                    UpdateDisplay();
                }

                if (takeOne.WasPressedThisFrame())
                {
                    if (selectedIndex >= 0 && selectedIndex < groupsFound.Count)
                    {
                        var gr = groupsFound[selectedIndex];
                        groupsWorldObjects.TryGetValue(gr, out var wo);

                        var inv = pc.GetPlayerBackpack().GetInventory();

                        if (stackSizeConfig != null 
                            && stackSizeConfig.Value > 1
                            && apiCanStack(currentInventory.GetId())
                            && (Keyboard.current[Key.LeftShift].isPressed || Keyboard.current[Key.RightShift].isPressed))
                        {
                            var wos = currentInventory.GetInsideWorldObjects();
                            int max = stackSizeConfig.Value;
                            for (int i = wos.Count - 1; i >= 0; i--)
                            {
                                var wo1 = wos[i];
                                var wo1gr = GeneticsGrouping.GetStackId(wo1);
                                if (wo1gr == gr)
                                {
                                    InventoriesHandler.Instance.TransferItem(currentInventory, inv, wo1);
                                    max--;
                                }
                                if (max <= 0)
                                {
                                    break;
                                }
                            }
                        }
                        else
                        if (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed)
                        {
                            InventoriesHandler.Instance.TransferAllSameGroup(currentInventory, inv, wo.GetGroup());
                        }
                        else
                        {
                            InventoriesHandler.Instance.TransferItem(currentInventory, inv, wo);
                        }
                    }
                }
                else
                if (takeAll.WasPressedThisFrame())
                {
                    if (selectedIndex >= 0 && selectedIndex < groupsFound.Count)
                    {
                        var inv = pc.GetPlayerBackpack().GetInventory();

                        InventoriesHandler.Instance.TransferAllItems(currentInventory, inv);
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnRotateObjectDispatcher))]
        static bool PlayerInputDispatcher_OnRotateObjectDispatcher()
        {
            return panel == null || !panel.activeSelf;
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBindings();
            Destroy(panel);
        }

        static void UpdateKeyBindings()
        {
            if (!keyTakeOne.Value.StartsWith("<"))
            {
                keyTakeOne.Value = "<Keyboard>/" + keyTakeOne.Value;
            }
            takeOne = new InputAction("Quick Loot Take One", binding: keyTakeOne.Value);
            takeOne.Enable();

            if (!keyTakeAll.Value.StartsWith("<"))
            {
                keyTakeAll.Value = "<Keyboard>/" + keyTakeAll.Value;
            }
            takeAll = new InputAction("Quick Loot Take One", binding: keyTakeAll.Value);
            takeAll.Enable();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
        Dictionary<string, Dictionary<string, string>> ___localizationDictionary
)
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["QuickLoot_TakeOne"] = "Egy elvétele";
                dict["QuickLoot_TakeStack"] = "Köteg elvétele";
                dict["QuickLoot_TakeRow"] = "Egész sor elvétele";
                dict["QuickLoot_TakeAll"] = "Minden elvétele";
                dict["QuickLoot_Empty"] = "Üres";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["QuickLoot_TakeOne"] = "Take One";
                dict["QuickLoot_TakeStack"] = "Take Stack";
                dict["QuickLoot_TakeRow"] = "Take Entire Row";
                dict["QuickLoot_TakeAll"] = "Take All";
                dict["QuickLoot_Empty"] = "Empty";
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            bool active = ___uisToHide[0].activeSelf;
            if (panel != null && !active)
            {
                panel.SetActive(false);
            }
        }


        internal class PanelEntry
        {
            internal GameObject rowObject;
            internal RectTransform rowRt;
            internal Image rowBackground;
            internal Image icon;
            internal RectTransform iconRt;
            internal Text amount;
            internal RectTransform amountRt;
            internal Text name;
            internal RectTransform nameRt;
        }
    }
}
