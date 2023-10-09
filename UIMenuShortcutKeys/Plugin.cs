using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;

namespace UIMenuShortcutKeys
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uimenushortcutkeys", "(UI) Menu Shortcut Keys", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        ConfigEntry<int> fontSize;
        ConfigEntry<string> configBuildToggleFilter;
        ConfigEntry<string> configContainerTakeAll;
        ConfigEntry<string> configSortPlayerInventory;
        ConfigEntry<string> configSortOtherInventory;
        ConfigEntry<bool> debugMode;

        GameObject ourCanvas;
        GameObject shortcutBar;

        InputAction buildToggleFilter;
        static bool buildToggleFilterState;

        InputAction containerTakeAll;

        InputAction sortPlayerInventory;
        InputAction sortOtherInventory;


        static FieldInfo playerEquipmentHasCleanConstructionChip;

        readonly List<ShortcutDisplayEntry> entries = new();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size");

            {
                configBuildToggleFilter = Config.Bind("General", "BuildToggleFilter", "<Keyboard>/F", "Toggle the tier-filter microchip's effect in the build screen");

                buildToggleFilter = new InputAction(name: "Toggle tier filter", binding: configBuildToggleFilter.Value);
                buildToggleFilter.Enable();
            }

            {
                configContainerTakeAll = Config.Bind("General", "ContainerTakeAll", "<Keyboard>/R", "Take everything from the currently open container");

                containerTakeAll = new InputAction(name: "Take All", binding: configContainerTakeAll.Value);
                containerTakeAll.Enable();
            }

            {
                configSortPlayerInventory = Config.Bind("General", "SortPlayerInventory", "<Keyboard>/G", "Sort the player's inventory");

                sortPlayerInventory = new InputAction(name: "Sort Player Inventory", binding: configSortPlayerInventory.Value);
                sortPlayerInventory.Enable();
            }

            {
                configSortOtherInventory = Config.Bind("General", "SortOtherInventory", "<Keyboard>/T", "Sort the other inventory");

                sortOtherInventory = new InputAction(name: "Sort Player Inventory", binding: configSortOtherInventory.Value);
                sortOtherInventory.Enable();
            }

            debugMode = Config.Bind("General", "DebugMode", false, "Turn this true to see log messages.");

            playerEquipmentHasCleanConstructionChip = AccessTools.Field(typeof(PlayerEquipment), "hasCleanConstructionChip");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }

            var ui = wh.GetOpenedUi();

            if (ui == DataConfig.UiType.Null)
            {
                DestroyInfoBar();
                return;
            }

            var w = wh.GetWindowViaUiId(ui);

            if (shortcutBar != null)
            {
                HandleKeys(ui, w);
                return;
            }

            var canvas = w.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = w.GetComponentInParent<Canvas>();
            }

            if (canvas == null)
            {
                if (debugMode.Value)
                {
                    Logger.LogWarning("Window canvas not found?!");
                }
                return;
            }

            ourCanvas = new GameObject("MenuShortcutKeysCanvas");
            var c = ourCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            //ourCanvas.transform.SetParent(canvas.transform);
            ourCanvas.transform.SetAsLastSibling();

            if (debugMode.Value)
            {
                Logger.LogInfo("Creating ShortcutBar for " + ui);
            }

            shortcutBar = new GameObject("MenuShortcutKeysInfoBar");
            shortcutBar.transform.SetParent(ourCanvas.transform, false);


            if (debugMode.Value)
            {
                Logger.LogInfo("  Creating background");
            }
            var background = shortcutBar.AddComponent<Image>();
            background.color = new Color(0, 0, 0, 0.95f);
            var rect = background.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0, 0, 0);
            rect.sizeDelta = new Vector2(100, 100);

            if (debugMode.Value)
            {
                Logger.LogInfo("  Creating entries");
            }
            entries.Clear();
            CreateEntriesForUI(ui, w);

            LayoutEntries();
            if (debugMode.Value)
            {
                Logger.LogInfo("  Done");
            }
        }

        void LayoutEntries()
        {
            if (debugMode.Value)
            {
                Logger.LogInfo("  Calculating sizes");
            }
            var margin = 10f;
            var sumWidth = 0f;
            float maxHeight = fontSize.Value;
            foreach (var entry in entries)
            {
                sumWidth += entry.preferredWidth;
                maxHeight = Mathf.Max(maxHeight, entry.preferredHeight);
            }
            if (entries.Count > 1)
            {
                sumWidth += entries.Count * margin;
            }

            RectTransform rect;

            if (debugMode.Value)
            {
                Logger.LogInfo("  Laying out shortcut entries");
            }
            var x = 0 - sumWidth / 2;
            foreach (var entry in entries)
            {
                rect = entry.gameObject.AddComponent<RectTransform>();

                rect.localPosition = new Vector3(x, 0, 0);

                x += entry.preferredWidth + margin;
            }

            if (debugMode.Value)
            {
                Logger.LogInfo("  Positioning to screen-bottom");
            }
            rect = shortcutBar.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(0, -Screen.height / 2 + maxHeight / 2 + margin);
            rect.sizeDelta = new Vector2(sumWidth + margin * 2, maxHeight + margin);

            shortcutBar.SetActive(entries.Count != 0);
        }

        void DestroyInfoBar()
        {
            Destroy(shortcutBar);
            shortcutBar = null;
            Destroy(ourCanvas);
            ourCanvas = null;
            foreach (var e in entries)
            {
                e.Destroy();
            }
            entries.Clear();
        }

        void HandleKeys(DataConfig.UiType ui, UiWindow window)
        {
            if (ui == DataConfig.UiType.Construction)
            {
                if (buildToggleFilter.WasPressedThisFrame()) {
                    if (window is UiWindowConstruction uiWindowConstruction)
                    {
                        buildToggleFilterState = !buildToggleFilterState;

                        window.gameObject.SetActive(false);
                        window.gameObject.SetActive(true);
                        AccessTools.Method(typeof(UiWindowConstruction), "CreateGrid").Invoke(uiWindowConstruction, new object[0]);
                    }
                    else
                    if (debugMode.Value)
                    {
                        Logger.LogWarning("Unknown container-type window: " + window.GetType());
                    }
                }
            }
            else if (ui == DataConfig.UiType.Container || ui == DataConfig.UiType.GroupSelector)
            {
                if (window is UiWindowContainer uiWindowContainer)
                {
                    if (containerTakeAll.WasPressedThisFrame())
                    {
                        var invD = uiWindowContainer.containerInventoryContainer.GetComponentInChildren<InventoryDisplayer>();

                        if (invD.iconMoveAll.activeSelf)
                        {
                            invD.OnMoveAll();
                        }
                    }
                    else if (sortPlayerInventory.WasPressedThisFrame())
                    {
                        var invD = uiWindowContainer.playerInventoryContainer.GetComponentInChildren<InventoryDisplayer>();
                        invD.OnSortInventory();
                    }
                    else if (sortOtherInventory.WasPressedThisFrame())
                    {
                        var invD = uiWindowContainer.containerInventoryContainer.GetComponentInChildren<InventoryDisplayer>();
                        invD.OnSortInventory();
                    }
                }
                else
                if (debugMode.Value)
                {
                    Logger.LogWarning("Unknown container-type window: " + window.GetType());
                }
            }
            else if (ui == DataConfig.UiType.Equipment)
            {
                if (sortPlayerInventory.WasPressedThisFrame())
                {
                    Managers.GetManager<PlayersManager>()
                        .GetActivePlayerController()
                        .GetPlayerBackpack()
                        .GetInventory()
                        .AutoSort();
                } 
                else if (sortOtherInventory.WasPressedThisFrame())
                {
                    Managers.GetManager<PlayersManager>()
                        .GetActivePlayerController()
                        .GetPlayerEquipment()
                        .GetInventory()
                        .AutoSort();
                }
            }
            else if (ui == DataConfig.UiType.Genetics)
            {
                if (sortPlayerInventory.WasPressedThisFrame())
                {
                    Managers.GetManager<PlayersManager>()
                        .GetActivePlayerController()
                        .GetPlayerBackpack()
                        .GetInventory()
                        .AutoSort();
                }
            }
        }

        void CreateEntriesForUI(DataConfig.UiType ui, UiWindow window)
        {
            if (ui == DataConfig.UiType.Construction)
            {
                var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerEquipment();
                if (pm.GetHasCleanConstructionChip())
                {
                    entries.Add(CreateEntry("BuildToggleFilter", configBuildToggleFilter.Value.Replace("<Keyboard>/", ""), "Toggle Tier Filter", shortcutBar.transform));
                }
            }
            else if (ui == DataConfig.UiType.Container || ui == DataConfig.UiType.GroupSelector)
            {
                if (window is UiWindowContainer uiWindowContainer)
                {
                    var invD = uiWindowContainer.containerInventoryContainer.GetComponentInChildren<InventoryDisplayer>();

                    if (invD.iconMoveAll.activeSelf)
                    {
                        entries.Add(CreateEntry("ContainerTakeAll", configContainerTakeAll.Value.Replace("<Keyboard>/", ""), "Take All", shortcutBar.transform));
                    }
                }
                else
                if (debugMode.Value)
                {
                    Logger.LogWarning("Unknown container-type window: " + window.GetType());
                }

                entries.Add(CreateEntry("SortPlayerInventory", configSortPlayerInventory.Value.Replace("<Keyboard>/", ""), "Sort Backpack", shortcutBar.transform));
                entries.Add(CreateEntry("SortOtherInventory", configSortOtherInventory.Value.Replace("<Keyboard>/", ""), "Sort Container", shortcutBar.transform));
            }
            else if (ui == DataConfig.UiType.Equipment)
            {
                entries.Add(CreateEntry("SortPlayerInventory", configSortPlayerInventory.Value.Replace("<Keyboard>/", ""), "Sort Backpack", shortcutBar.transform));
                entries.Add(CreateEntry("SortOtherInventory", configSortOtherInventory.Value.Replace("<Keyboard>/", ""), "Sort Equipment", shortcutBar.transform));
            }
            else if (ui == DataConfig.UiType.Genetics)
            {
                entries.Add(CreateEntry("SortPlayerInventory", configSortPlayerInventory.Value.Replace("<Keyboard>/", ""), "Sort Backpack", shortcutBar.transform));
            }
        }

        static bool originalHasFilter;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowConstruction), "CreateGrid")]
        static void UiWindowConstruction_CreateGrid()
        {
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerEquipment();
            originalHasFilter = pm.GetHasCleanConstructionChip();
            playerEquipmentHasCleanConstructionChip.SetValue(pm, buildToggleFilterState && originalHasFilter);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowConstruction), "CreateGrid")]
        static void UiWindowConstruction_CreateGrid_Post()
        {
            var pm = Managers.GetManager<PlayersManager>().GetActivePlayerController().GetPlayerEquipment();
            playerEquipmentHasCleanConstructionChip.SetValue(pm, originalHasFilter);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            buildToggleFilterState = Managers.GetManager<PlayersManager>()
                .GetActivePlayerController()
                .GetPlayerEquipment()
                .GetHasCleanConstructionChip();
        }

        class ShortcutDisplayEntry
        {
            internal GameObject gameObject;

            internal string tag;

            internal float preferredWidth;
            internal float preferredHeight;

            internal void Destroy()
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        ShortcutDisplayEntry CreateEntry(string tag, string key, string description, Transform parent)
        {
            ShortcutDisplayEntry entry = new();
            entry.tag = tag;
            entry.gameObject = new GameObject("ShortcutDisplayEntry-" + key);
            entry.gameObject.transform.SetParent(parent);

            // The keyboard shortcut text

            var keyText = new GameObject("KeyText");

            var txt = keyText.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = key;
            txt.color = Color.white;
            txt.fontSize = fontSize.Value;
            txt.resizeTextForBestFit = false;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleCenter;

            var keyW = txt.preferredWidth;
            var keyH = txt.preferredHeight;

            var keyMargin = 4;

            var rect = txt.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(keyMargin + keyW / 2, 0, 0);
            rect.sizeDelta = new Vector2(keyW, keyH);

            // background1 for the shorcut

            var keyBackground1 = new GameObject("KeyBackground1");
            keyBackground1.transform.SetParent(entry.gameObject.transform, false);

            var img = keyBackground1.AddComponent<Image>();
            img.color = Color.white;
            rect = img.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(keyMargin + keyW / 2, 0, 0);
            rect.sizeDelta = new Vector2(keyW + keyMargin * 2, keyH + keyMargin * 2);

            // background for the shortcut

            var keyBackground2 = new GameObject("KeyBackground1");
            keyBackground2.transform.SetParent(entry.gameObject.transform, false);

            img = keyBackground2.AddComponent<Image>();
            img.color = Color.black;
            rect = img.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(keyMargin + keyW / 2, 0, 0);
            rect.sizeDelta = new Vector2(keyW + keyMargin * 2 - 2, keyH + keyMargin * 2 - 2);

            keyText.transform.SetParent(entry.gameObject.transform, false);

            // shortcut description

            var descriptionText = new GameObject("DescriptionText");

            txt = descriptionText.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = description;
            txt.color = Color.white;
            txt.fontSize = fontSize.Value;
            txt.resizeTextForBestFit = false;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleCenter;

            var descW = txt.preferredWidth;
            var descH = txt.preferredHeight;
            rect = txt.GetComponent<RectTransform>();
            rect.localPosition = new Vector3(10 + keyMargin * 2 + keyW + descW / 2, 0, 0);
            rect.sizeDelta = new Vector2(descW, descH);

            entry.preferredWidth = descW + keyW + 10 + 2 * keyMargin;
            entry.preferredHeight = Mathf.Max(descH, keyH) + 10;

            descriptionText.transform.SetParent(entry.gameObject.transform, false);

            return entry;
        }
    }
}
