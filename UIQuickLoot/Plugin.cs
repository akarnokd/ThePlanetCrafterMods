// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using System.Reflection;
using BepInEx.Configuration;
using UnityEngine;
using BepInEx.Logging;
using System.Linq;
using UnityEngine.UI;
using LibCommon;
using UnityEngine.InputSystem;
using System.Text;

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

        static ConfigEntry<string> keyTakeOne;
        static ConfigEntry<string> keyTakeAll;

        static Font font;

        static Color normalRowColor = new(0.1f, 0.1f, 0.1f, 0);
        static Color selectedRowColor = new(0.5f, 0f, 5.0f, 0.95f);

        static InputAction takeOne;
        static InputAction takeAll;

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

            panelX = Config.Bind("UI", "PanelX", 100, "Shift the panel in the X direction by this amount relative to screen center.");
            panelY = Config.Bind("UI", "PanelY", 0, "Shift the panel in the Y direction by this amount relative to screen center.");
            panelWidth = Config.Bind("UI", "PanelWidth", 450, "The width of the panel.");
            panelOpacity = Config.Bind("UI", "PanelOpacity", 0.99f, "The opacity: 1 - fully opaque, 0 fully transparent.");
            rowCount = Config.Bind("UI", "RowCount", 9, "The number of rows to display at once.");
            rowHeight = Config.Bind("UI", "RowHeight", 32, "The height of rows in pixels.");
            fontSize = Config.Bind("UI", "FontSize", 24, "The font size");
            margin = Config.Bind("UI", "Margin", 5, "The margin between visual elements.");
            amountWidth = Config.Bind("UI", "AmountWidth", 100, "The width of the amount field.");

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
            ActionOpenable __instance, 
            BaseHudHandler ____hudHandler)
        {
            if (!modEnabled.Value)
            {
                return;
            }
            var inventoryAssoc = __instance.GetComponent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return;
            }

            var inventoryAssocProxy = __instance.GetComponent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
                return;
            }

            inventoryAssoc = __instance.GetComponentInParent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return;
            }

            inventoryAssocProxy = __instance.GetComponentInParent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
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
            ActionOpenable __instance,
            BaseHudHandler ____hudHandler)
        {
            if (!modEnabled.Value)
            {
                return;
            }
            var inventoryAssoc = __instance.GetComponent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return;
            }

            var inventoryAssocProxy = __instance.GetComponent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
                return;
            }

            inventoryAssoc = __instance.GetComponentInParent<InventoryAssociated>();
            if (inventoryAssoc != null)
            {
                inventoryAssoc.GetInventory(inv => OnInventory(inv, __instance, ____hudHandler));
                return;
            }

            inventoryAssocProxy = __instance.GetComponentInParent<InventoryAssociatedProxy>();
            if (inventoryAssocProxy != null)
            {
                inventoryAssocProxy.GetInventory((inv, _) => OnInventory(inv, __instance, ____hudHandler));
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

        static readonly Action<WorldObject, bool> OnInventoyModified = (wo, add) => UpdateDisplay();

        static void OnInventory(Inventory inventory, Actionnable __instance,
            BaseHudHandler ____hudHandler)
        {
            currentActionable = __instance;
            if (currentInventory != null)
            {
                currentInventory.inventoryContentModified -= OnInventoyModified;
            }
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
            if (wh.GetHasUiOpen())
            {
                panel.SetActive(false);
            }

            if (panel == null || !panel.activeSelf)
            {
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
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["QuickLoot_TakeOne"] = "Take One";
                dict["QuickLoot_TakeStack"] = "Take Stack";
                dict["QuickLoot_TakeRow"] = "Take Entire Row";
                dict["QuickLoot_TakeAll"] = "Take All";
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
