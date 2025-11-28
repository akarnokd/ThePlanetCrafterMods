// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine.InputSystem.Controls;
using Unity.Netcode;
using Tessera;
using UnityEngine.UI;
using System;

namespace CheatMinimap
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatminimap", "(Cheat) Minimap", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        Texture2D grid;
        Texture2D barren;
        Texture2D lush;
        Texture2D endgame;
        Texture2D marker;
        Texture2D marker2;
        Texture2D chest;
        Texture2D golden;
        Texture2D starform;
        Texture2D ladder;
        Texture2D server;
        Texture2D below;
        Texture2D above;
        Texture2D safe;
        Texture2D outOfBoundsTexture;
        Texture2D portal;
        Texture2D altar;
        Texture2D stair;
        Texture2D drone;
        Texture2D wreck;
        Texture2D furniture;
        Texture2D poster;
        Texture2D fusion;
        Texture2D animal;
        Texture2D humbleBarren;
        Texture2D humbleLush;
        Texture2D seleneaBarren;
        Texture2D seleneaLush;
        Texture2D aqualisBarren;
        Texture2D aqualisLush;
        Texture2D toxicityBarren;
        Texture2D toxicityLush;

        ConfigEntry<int> mapSize;
        ConfigEntry<int> mapBottom;
        ConfigEntry<int> mapPanelLeft;
        ConfigEntry<int> zoomLevel;
        ConfigEntry<int> maxZoomLevel;
        static ConfigEntry<string> toggleKey;
        ConfigEntry<int> zoomInMouseButton;
        ConfigEntry<int> zoomOutMouseButton;
        ConfigEntry<int> autoScanForChests;
        ConfigEntry<int> fixedRotation;
        ConfigEntry<float> alphaBlend;

        static ConfigEntry<bool> showLadders;
        static ConfigEntry<bool> showServers;
        static ConfigEntry<bool> showSafes;
        static ConfigEntry<bool> showAltars;
        static ConfigEntry<bool> showStairs;
        static ConfigEntry<bool> showDrones;
        static ConfigEntry<bool> showWreckDeconstructibles;
        static ConfigEntry<bool> showWreckFurniture;
        static ConfigEntry<bool> showWreckPoster;
        static ConfigEntry<bool> showWreckAnimalEffigie;
        static ConfigEntry<bool> showWreckFusion;

        static ConfigEntry<bool> mapManualVisible;
        static ConfigEntry<int> fontSize;

        static ConfigEntry<string> outOfBoundsColor;
        static string lastOutOfBoundsColor;

        static ConfigEntry<string> toggleXRay;
        static ConfigEntry<bool> xRay;
        static ConfigEntry<int> xRayRange;
        static Font xRayFont;
        static InputAction xRayAction;

        static bool mapVisible = true;
        static int autoScanEnabled = 0;
        static bool coroutineRunning = false;

        static InputAction mapAction;

        static Plugin self;

        static readonly Dictionary<string, RectMinMax> mapRects = new()
        {
            { "Prime", new RectMinMax(-2000, -2000, 3000, 3000) },
            { "Humble", new RectMinMax(-2300, -2300, 2700, 2700) },
            { "Selenea", new RectMinMax(-2000, -2000, 3000, 3000) },
            { "Aqualis", new RectMinMax(-3000, -3000, 4000, 4000) },
            { "Toxicity", new RectMinMax(-2200, -2200, 2800, 2800) },
        };

        internal class RectMinMax
        {
            internal int minX;
            internal int maxX;
            internal int minY;
            internal int maxY;
            internal RectMinMax(int minX, int minY, int maxX, int maxY)
            {
                this.minX = minX;
                this.maxX = maxX;
                this.minY = minY;
                this.maxY = maxY;
            }
        }

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            self = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            grid = LoadPNG(Path.Combine(dir, "map_grid.png"));
            barren = LoadPNG(Path.Combine(dir, "map_barren.png"));
            lush = LoadPNG(Path.Combine(dir, "map_lush.png"));
            endgame = LoadPNG(Path.Combine(dir, "map_endgame.png"));
            marker = LoadPNG(Path.Combine(dir, "player_marker.png"));
            marker2 = LoadPNG(Path.Combine(dir, "player_marker_2.png"));
            chest = LoadPNG(Path.Combine(dir, "chest.png"));
            golden = LoadPNG(Path.Combine(dir, "chest_golden.png"));
            starform = LoadPNG(Path.Combine(dir, "chest_starform.png"));
            ladder = LoadPNG(Path.Combine(dir, "ladder.png"));
            server = LoadPNG(Path.Combine(dir, "server.png"));
            above = LoadPNG(Path.Combine(dir, "above.png"));
            below = LoadPNG(Path.Combine(dir, "below.png"));
            safe = LoadPNG(Path.Combine(dir, "safe.png"));
            portal = LoadPNG(Path.Combine(dir, "portal.png"));
            outOfBoundsTexture = new Texture2D(1, 1);
            altar = LoadPNG(Path.Combine(dir, "altar.png"));
            stair = LoadPNG(Path.Combine(dir, "stair.png"));
            drone = LoadPNG(Path.Combine(dir, "drone.png"));
            humbleBarren = LoadPNG(Path.Combine(dir, "humble_barren.png"));
            humbleLush = LoadPNG(Path.Combine(dir, "humble_lush.png"));
            seleneaBarren = LoadPNG(Path.Combine(dir, "selenea_barren.png"));
            seleneaLush = LoadPNG(Path.Combine(dir, "selenea_lush.png"));
            aqualisBarren = LoadPNG(Path.Combine(dir, "aqualis_barren.png"));
            aqualisLush = LoadPNG(Path.Combine(dir, "aqualis_lush.png"));
            wreck = LoadPNG(Path.Combine(dir, "wreck.png"));
            furniture = LoadPNG(Path.Combine(dir, "furniture.png"));
            poster = LoadPNG(Path.Combine(dir, "poster.png"));
            animal = LoadPNG(Path.Combine(dir, "animal.png"));
            fusion = LoadPNG(Path.Combine(dir, "fusion.png"));
            toxicityBarren = LoadPNG(Path.Combine(dir, "toxicity_barren.jpg"));
            toxicityLush = LoadPNG(Path.Combine(dir, "toxicity_lush.jpg"));

            mapSize = Config.Bind("General", "MapSize", 400, "The minimap panel size");
            mapBottom = Config.Bind("General", "MapBottom", 350, "Panel position from the bottom of the screen");
            mapPanelLeft = Config.Bind("General", "MapLeft", 0, "Panel position from the left of the screen");
            zoomLevel = Config.Bind("General", "ZoomLevel", 4, "The zoom level");
            maxZoomLevel = Config.Bind("General", "MaxZoomLevel", 13, "The maximum zoom level");
            toggleKey = Config.Bind("General", "ToggleKey", "N", "The key to press to toggle the minimap");
            zoomInMouseButton = Config.Bind("General", "ZoomInMouseButton", 4, "Which mouse button to use for zooming in (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");
            zoomOutMouseButton = Config.Bind("General", "ZoomOutMouseButton", 5, "Which mouse button to use for zooming out (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");
            autoScanForChests = Config.Bind("General", "AutoScanForChests", 5, "If nonzero and the minimap is visible, the minimap periodically scans for chests every N seconds. Toggle with Alt+N");
            fixedRotation = Config.Bind("General", "FixedRotation", -1, "If negative, the map rotates on screen. If Positive, the map is fixed to that rotation in degrees (0..360).");
            mapManualVisible = Config.Bind("General", "MapVisible", true, "Should the map be visible?");
            fontSize = Config.Bind("General", "FontSize", 16, "The size of the names of other players, use 0 to disable showing their name.");
            showLadders = Config.Bind("General", "ShowWreckLadders", true, "Show the ladders in the procedural wrecks?");
            showServers = Config.Bind("General", "ShowServers", true, "Show the server racks?");
            showSafes = Config.Bind("General", "ShowSafes", true, "Show the wreck safes?");
            outOfBoundsColor = Config.Bind("General", "OutOfBoundsColor", "255,127,106,0", "The color of the out-of-bounds area as ARGB ints of range 0-255");
            alphaBlend = Config.Bind("General", "AlphaBlend", 1f, "Specify the alpha-opacity level of the map. 1 - opaque, 0.5 - half transparent, 0 - invisible");
            showAltars = Config.Bind("General", "ShowAltars", true, "Show the Warden Altars?");
            showStairs = Config.Bind("General", "ShowStairs", true, "Show the stairs in procedural wrecks?");
            showDrones = Config.Bind("General", "ShowDrones", true, "Show the lootable drones?");
            showWreckDeconstructibles = Config.Bind("General", "ShowWreckDeconstructibles", true, "Show all deconstructibles inside wrecks?");
            showWreckFurniture = Config.Bind("General", "ShowWreckFurniture", true, "Show wreck furniture?");
            showWreckPoster = Config.Bind("General", "ShowWreckPoster", true, "Show wreck posters?");
            showWreckAnimalEffigie = Config.Bind("General", "ShowWreckAnimalEffigie", true, "Show wreck animal effigies?");
            showWreckFusion = Config.Bind("General", "ShowWreckFusion", true, "Show wreck fusion generators?");
            toggleXRay = Config.Bind("General", "ToggleXRay", "<Keyboard>/comma", "The key to toggle an overlay showing items of interest through walls/terrain.");
            xRay = Config.Bind("General", "XRay", false, "Is the XRay mode on, an overlay showing items of interest through walls/terrain?");
            xRayRange = Config.Bind("General", "XRayRange", 50, "The range to look for items in XRay mode");
            
            xRayFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            UpdateKeyBinding();

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static ButtonControl MouseButtonForIndex(int index)
        {
            return index switch
            {
                1 => Mouse.current.leftButton,
                2 => Mouse.current.rightButton,
                3 => Mouse.current.middleButton,
                4 => Mouse.current.forwardButton,
                5 => Mouse.current.backButton,
                _ => null,
            };
        }

        static readonly List<GameObject> chests = [];

        IEnumerator AutoScan()
        {
            for (; ; )
            {
                int n = autoScanForChests.Value;
                if (mapManualVisible.Value && n > 0 && autoScanEnabled == 1)
                {
                    FindChests();
                }
                else
                {
                    n = 5;
                }
                yield return new WaitForSeconds(n);
            }
        }

        void Update()
        {
            PlayersManager pm = Managers.GetManager<PlayersManager>();
            PlayerMainController player = pm?.GetActivePlayerController();
            WindowsHandler wh = Managers.GetManager<WindowsHandler>();
            var windowNotOpen = wh != null && !wh.GetHasUiOpen();
            if (player != null && windowNotOpen)
            {
                if (!coroutineRunning)
                {
                    coroutineRunning = true;
                    StartCoroutine(AutoScan());
                }

                if (MouseButtonForIndex(zoomInMouseButton.Value)?.wasPressedThisFrame ?? false)
                {
                    zoomLevel.Value = Mathf.Clamp(zoomLevel.Value + 1, 1, maxZoomLevel.Value);
                }
                if (MouseButtonForIndex(zoomOutMouseButton.Value)?.wasPressedThisFrame ?? false)
                {
                    zoomLevel.Value = Mathf.Clamp(zoomLevel.Value - 1, 1, maxZoomLevel.Value);
                }

                if (mapAction.WasPressedThisFrame())
                {
                    if (Keyboard.current[Key.LeftShift].isPressed)
                    {
                        zoomLevel.Value = Mathf.Clamp(zoomLevel.Value + 1, 1, maxZoomLevel.Value);
                    }
                    else
                    if (Keyboard.current[Key.LeftCtrl].isPressed)
                    {
                        zoomLevel.Value = Mathf.Clamp(zoomLevel.Value - 1, 1, maxZoomLevel.Value);
                    }
                    else
                    if (Keyboard.current[Key.LeftAlt].isPressed)
                    {
                        if (autoScanForChests.Value > 0)
                        {
                            autoScanEnabled = (autoScanEnabled + 1) % 3;
                            if (autoScanEnabled == 0)
                            {
                                chests.Clear();
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("Minimap_DisableChestAutoScan", 2f);
                            }
                            else
                            if (autoScanEnabled == 1)
                            {
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("Minimap_BeginChestAutoScan", 2f);
                                FindChests();
                            }
                            else
                            {
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, 
                                    string.Format(Localization.GetLocalizedString("Minimap_StopChestAutoScan"), chests.Count));
                            }
                        } 
                        else
                        if (chests.Count != 0)
                        {
                            chests.Clear();
                        }
                        else
                        {
                            FindChests();
                        }
                    }
                    else
                    {
                        mapManualVisible.Value = !mapManualVisible.Value;
                    }
                }
            }
            UpdateXRay(player, windowNotOpen);
        }

        static GameObject xrayCanvas;


        void UpdateXRay(PlayerMainController player, bool windowNotOpen)
        {
            if (xRayAction.WasPressedThisFrame())
            {
                xRay.Value = !xRay.Value;
            }
            if (!xRay.Value || player == null)
            {
                if (xrayCanvas != null)
                {
                    Destroy(xrayCanvas);
                    xrayCanvas = null;
                }
                return;
            }

            if (xrayCanvas == null)
            {
                xrayCanvas = new GameObject("Minimap_XRay");
                var canvas = xrayCanvas.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;
            }

            xrayCanvas.SetActive(windowNotOpen);

            List<GameObject> chestsInRange = [];
            foreach (var chest in chests)
            {
                if (chest != null && Vector3.Distance(chest.transform.position, player.transform.position) <= xRayRange.Value)
                {
                    chestsInRange.Add(chest);
                }
            }

            while (xrayCanvas.transform.childCount < chestsInRange.Count)
            {
                var entry = new GameObject("XRayElement");
                entry.SetActive(false);
                entry.transform.SetParent(xrayCanvas.transform, false);
                var text = entry.AddComponent<Text>();
                text.fontSize = fontSize.Value;
                text.font = xRayFont;
                text.color = Color.white;
                text.supportRichText = true;
                text.alignment = TextAnchor.MiddleCenter;

                var outline = entry.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(1, -1);
            }
            for (int i = xrayCanvas.transform.childCount - 1; i >= chestsInRange.Count; i--)
            {
                Destroy(xrayCanvas.transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < chestsInRange.Count; i++)
            {
                GameObject chest = chestsInRange[i];
                var xrayChild = xrayCanvas.transform.GetChild(i).gameObject;
                var text = xrayChild.GetComponent<Text>();

                var pos = chest.transform.position;
                text.text = chest.name.Replace("(Clone)", "") + "\n(" + ((int)Vector3.Distance(player.transform.position, pos)) + " m)";

                var xy = Camera.main.WorldToScreenPoint(pos) - new Vector3(Screen.width / 2, Screen.height / 2, 0);
                var heading = pos - Camera.main.transform.position;
                var behind = Vector3.Dot(Camera.main.transform.forward, heading) < 0;

                if (!behind)
                {
                    var rt = text.GetComponent<RectTransform>();
                    rt.localPosition = xy;
                    rt.sizeDelta = new Vector2(text.preferredWidth, text.preferredHeight);
                }

                xrayChild.SetActive(!behind);
            }

        }

        void FindChests()
        {
            chests.Clear();

            var ih = ProceduralInstancesHandler.Instance;

            foreach (ActionOpenable ia in FindObjectsByType<ActionOpenable>(FindObjectsSortMode.None))
            {
                try
                {
                    var go = ia.gameObject;
                    var parentName = go.transform.parent != null ? go.transform.parent.name : "";

                    if ((go.name.Contains("WorldContainer", StringComparison.Ordinal) && !parentName.StartsWith("WorldRoverWithContainer", StringComparison.Ordinal))
                        || go.name.Contains("GoldenContainer", StringComparison.Ordinal) 
                        || go.name.Contains("WorldCanister", StringComparison.Ordinal)
                        || go.name.Contains("WreckContainer", StringComparison.Ordinal)
                        || go.name.Contains("WreckCanister", StringComparison.Ordinal)
                    )
                    {

                        chests.Add(go);
                    }
                    else if (
                        ((go.name.Contains("WreckSafe", StringComparison.Ordinal) || go.name.Contains("WorldSafe", StringComparison.Ordinal)) && showSafes.Value)
                        || (go.name.Contains("Warden") && showAltars.Value)
                        || (showDrones.Value && (!go.name.Contains("Clone", StringComparison.Ordinal) && go.name.StartsWith("Drone", StringComparison.Ordinal) && go.name.Length > 5))
                        || go.name.Contains("WorldWardrobe", StringComparison.Ordinal)
                        || go.name.Contains("Satellite", StringComparison.Ordinal)
                        || (go.name.Contains("WorldContainer", StringComparison.Ordinal) && parentName.StartsWith("WorldRoverWithContainer", StringComparison.Ordinal))
                        || (go.name.Contains("FusionGenerator", StringComparison.Ordinal) && showWreckFusion.Value)
                    )
                    {
                        var invAssoc = go.GetComponentInParent<InventoryAssociated>();
                        var invAssocProxy = go.GetComponentInParent<InventoryAssociatedProxy>();
                        var hideWhenFull = go.name.Contains("FusionGenerator", StringComparison.Ordinal);

                        if (invAssoc != null && (invAssocProxy == null || (NetworkManager.Singleton?.IsServer ?? false)))
                        {
                            var id = invAssoc.GetInventoryId();
                            if (id > 0)
                            {
                                var inv = InventoriesHandler.Instance.GetInventoryById(id);
                                if (ShouldShowIfNonEmptyOrNotFull(inv, hideWhenFull))
                                {
                                    chests.Add(go);
                                }
                            }
                            else if (go.GetComponentInParent<InventoryFromScene>() != null && id < 0)
                            {
                                if (go.TryGetComponent<WorldUniqueId>(out var wuid))
                                {
                                    var inv = InventoriesHandler.Instance.GetInventoryById(wuid.GetWorldUniqueId());
                                    if (inv == null || ShouldShowIfNonEmptyOrNotFull(inv, hideWhenFull))
                                    {
                                        chests.Add(go);
                                    }
                                }
                                else
                                {
                                    chests.Add(go);
                                }
                            }
                        }
                        else if (invAssocProxy != null)
                        {
                            var go1 = go;
                            invAssocProxy.GetInventory((inv, wo) =>
                            {
                                if (ShouldShowIfNonEmptyOrNotFull(inv, hideWhenFull))
                                {
                                    chests.Add(go1);
                                }
                            });
                        }
                    }
                }
                catch
                {
                    // some kind of despawn?
                }
            }
            if (showLadders.Value)
            {
                var ladderSet = new HashSet<GameObject>();
                foreach (var am in FindObjectsByType<ActionMovePlayer>(FindObjectsSortMode.None))
                {
                    if (am != null && am.transform.parent != null 
                        && am.transform.parent.parent != null
                        && am.transform.parent.parent.gameObject.name.Contains("LadderWreck", StringComparison.Ordinal))
                    {
                        var go = am.transform.parent.parent.gameObject;
                        if (ladderSet.Add(go))
                        {
                            chests.Add(go);
                        }
                    }
                    if (am != null && am.gameObject.name.Contains("ladder_", StringComparison.Ordinal))
                    {
                        var go = am.transform.parent.gameObject;
                        if (ladderSet.Add(go))
                        {
                            chests.Add(go);
                        }
                    }
                }
            }
            if (showServers.Value || showWreckDeconstructibles.Value
                || showWreckFurniture.Value)
            {
                foreach (var id in FindObjectsByType<ActionDeconstructible>(FindObjectsSortMode.None))
                {
                    if (showServers.Value)
                    {
                        if (id.transform.parent != null 
                            && (id.transform.parent.name.Contains("WreckServer", StringComparison.Ordinal)
                            || id.transform.parent.name.Contains("WorldServer", StringComparison.Ordinal)))
                        {
                            chests.Add(id.transform.parent.gameObject);
                        }
                    }
                    var tr = id.transform;

                    while (tr != null)
                    {

                        string name1 = tr.gameObject.name;
                        if (showWreckDeconstructibles.Value
                            && (name1.Contains("WreckPilar", StringComparison.Ordinal) || name1.Contains("WorldPilar", StringComparison.Ordinal)))
                        {
                            chests.Add(tr.gameObject);
                            break;
                        }
                        if (showWreckFurniture.Value
                            && (IsFurniture(name1))
                            && ih != null && ih.IsInsideAnInstance(tr.position, false)
                        )
                        {
                            chests.Add(tr.gameObject);
                            break;
                        }
                        if (showServers.Value
                            && name1.StartsWith("Server", StringComparison.Ordinal)
                            && ih != null && ih.IsInsideAnInstance(tr.position, false)
                        )
                        {
                            chests.Add(tr.gameObject);
                            break;
                        }
                        if (showSafes.Value
                            && name1.StartsWith("Vault", StringComparison.Ordinal)
                            && ih != null && ih.IsInsideAnInstance(tr.position, false)
                        )
                        {
                            chests.Add(tr.gameObject);
                            break;
                        }
                        tr = tr.parent;
                    }
                }
            }
            if (showWreckPoster.Value || showWreckAnimalEffigie.Value)
            {
                foreach (var id in FindObjectsByType<ActionGrabable>(FindObjectsSortMode.None))
                {
                    var tr = id.transform;

                    while (tr != null)
                    {
                        string name1 = tr.gameObject.name;
                        if (showWreckPoster.Value 
                            && name1.StartsWith("Poster", StringComparison.Ordinal)
                            && ih != null && ih.IsInsideAnInstance(tr.position, false)
                        )
                        {
                            chests.Add(tr.gameObject);
                            break;
                        }
                        if (showWreckAnimalEffigie.Value 
                            && name1.StartsWith("AnimalEffigie", StringComparison.Ordinal)
                            && ih != null && ih.IsInsideAnInstance(tr.position, false)
                        )
                        {
                            chests.Add(tr.gameObject);
                            break;
                        }
                        tr = tr.parent;
                    }
                }
            }
            
            foreach (var p in FindObjectsByType<MachinePortal>(FindObjectsSortMode.None))
            {
                chests.Add(p.gameObject);
            }

            if (showStairs.Value)
            {
                foreach (var id in FindObjectsByType<TesseraTile>(FindObjectsSortMode.None))
                {
                    if (id.name.Contains("Stair", StringComparison.Ordinal))
                    {
                        chests.Add(id.transform.gameObject);
                    }
                }
            }

            if (autoScanEnabled == 0)
            {
                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, 
                    string.Format(Localization.GetLocalizedString("Minimap_FoundChestAutoScan"), chests.Count));
            }
        }

        static bool ShouldShowIfNonEmptyOrNotFull(Inventory inv, bool checkFull)
        {
            if (inv == null)
            {
                return false;
            }
            int n = inv.GetInsideWorldObjects().Count;
            if (!checkFull)
            {
                return n != 0;
            }
            return n != inv.GetSize();
        }

        static bool IsFurniture(string name1)
        {
            return name1.StartsWith("Table", StringComparison.Ordinal)
                                || name1.StartsWith("Chair", StringComparison.Ordinal)
                                || name1.StartsWith("Counter", StringComparison.Ordinal)
                                || name1.StartsWith("Fridge", StringComparison.Ordinal)
                                || name1.StartsWith("Trashcan", StringComparison.Ordinal)
                                || name1.StartsWith("Faucet", StringComparison.Ordinal)
                                || name1.StartsWith("Desktop", StringComparison.Ordinal)
                                || name1.StartsWith("ExerciseBike", StringComparison.Ordinal)
                                || name1.StartsWith("FlowerPot", StringComparison.Ordinal)
                                || name1.StartsWith("Library", StringComparison.Ordinal)
                                || name1.StartsWith("PlanetViewer", StringComparison.Ordinal)
                                || name1.StartsWith("Pooltable", StringComparison.Ordinal)
                                || name1.StartsWith("Shelves", StringComparison.Ordinal)
                                || name1.StartsWith("Treadmill", StringComparison.Ordinal)
                                || name1.StartsWith("TreePlanter", StringComparison.Ordinal)
                                || name1.StartsWith("Ivy", StringComparison.Ordinal);
        }

        void OnGUI()
        {
            if (mapVisible && mapManualVisible.Value)
            {
                PlayersManager pm = Managers.GetManager<PlayersManager>();
                PlayerMainController player = pm?.GetActivePlayerController();
                WindowsHandler wh = Managers.GetManager<WindowsHandler>();
                if (player != null && wh != null && !wh.GetHasUiOpen())
                {
                    UpdateOutOfBoundsTexture();

                    int panelWidth = mapSize.Value;

                    float angle = player.transform.eulerAngles.y;
                    var minimapRect = new Rect(mapPanelLeft.Value, Screen.height - panelWidth - mapBottom.Value, panelWidth, panelWidth);
                    var mapCenter = new Vector2(panelWidth / 2, panelWidth / 2);
                    float zoom = zoomLevel.Value;

                    var playerY = player.transform.position.y;

                    Texture2D theMap = grid;

                    var pd = Managers.GetManager<PlanetLoader>()?.GetCurrentPlanetData();

                    RectMinMax mapMinMax = default;
                    if (pd == null || !mapRects.TryGetValue(pd.id, out mapMinMax))
                    {
                        mapMinMax = mapRects["Prime"];
                    }
                    var isPrime = pd != null && (pd.id == "" || pd.id == "Prime");

                    // calibrated to the given map
                    float playerCenterX = (mapMinMax.maxX + mapMinMax.minX) / 2;
                    float playerCenterY = (mapMinMax.maxY + mapMinMax.minY) / 2;
                    float mapWidth = mapMinMax.maxX - mapMinMax.minX;
                    float mapHeight = mapMinMax.maxY - mapMinMax.minY;


                    if (isPrime)
                    {
                        if (achievementsHandler != null && worldUnitsHandler != null)
                        {
                            var currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                            var minT = pd.startMossTerraStage.GetStageStartValue();
                            if (currT >= 425000000000f)
                            {
                                theMap = endgame;
                            }
                            else if (currT >= minT)
                            {
                                theMap = lush;
                            }
                            else
                            {
                                theMap = barren;
                            }
                        }
                        else
                        {
                            theMap = barren;
                        }
                    }
                    if (pd != null && pd.id == "Humble")
                    {
                        theMap = humbleBarren;

                        if (achievementsHandler != null && worldUnitsHandler != null)
                        {
                            var currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                            var minT = pd.startMossTerraStage.GetStageStartValue();
                            if (currT >= minT)
                            {
                                theMap = humbleLush;
                            }
                        }
                    }
                    if (pd != null && pd.id == "Selenea")
                    {
                        theMap = seleneaBarren;

                        if (achievementsHandler != null && worldUnitsHandler != null)
                        {
                            var currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                            var minT = pd.startMossTerraStage.GetStageStartValue();
                            if (currT >= minT)
                            {
                                theMap = seleneaLush;
                            }
                        }
                    }
                    if (pd != null && pd.id == "Aqualis")
                    {
                        theMap = aqualisBarren;

                        if (achievementsHandler != null && worldUnitsHandler != null)
                        {
                            var currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                            var minT = pd.startMossTerraStage.GetStageStartValue();
                            if (currT >= minT)
                            {
                                theMap = aqualisLush;
                            }
                        }
                    }
                    if (pd != null && pd.id == "Toxicity")
                    {
                        theMap = toxicityBarren;

                        if (achievementsHandler != null && worldUnitsHandler != null)
                        {
                            var currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                            var minT = pd.startMossTerraStage.GetStageStartValue();
                            if (currT >= minT)
                            {
                                theMap = toxicityLush;
                            }
                        }
                    }
                    float mapImageWidth = theMap.width;
                    float mapImageHeight = theMap.height;


                    // ^
                    // | z+
                    // |
                    // ------> x+

                    // how much the player moved off center, in pixels
                    float mapScaleX = mapImageWidth / mapWidth;
                    float mapScaleY = mapImageHeight / mapHeight;

                    float playerExcentricX = -(player.transform.position.x - playerCenterX) * mapScaleX;
                    float playerExcentricY = (player.transform.position.z - playerCenterY) * mapScaleY;

                    // how much to shrink to fit into the panel
                    float shrink;
                    if (mapImageWidth > mapImageHeight)
                    {
                        shrink = panelWidth / mapImageWidth;
                    }
                    else
                    {
                        shrink = panelWidth / mapImageHeight;
                    }

                    // default zoomed-in rectangle
                    float zw = zoom * shrink * mapImageWidth;
                    float zh = zoom * shrink * mapImageHeight;

                    // center on the player
                    float zx = playerExcentricX * shrink * zoom - zw / 2 + panelWidth / 2;
                    float zy = playerExcentricY * shrink * zoom - zh / 2 + panelWidth / 2;

                    int fixRot = fixedRotation.Value;
                    float fixedAngle = fixRot;

                    GUI.BeginGroup(minimapRect);

                    var unrotatedMatrix = GUI.matrix;
                    float rotateAround = fixRot >= 0 ? fixedAngle : -angle;
                    GUIUtility.RotateAroundPivot(rotateAround, mapCenter);

                    var colorSaveTemp = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, alphaBlend.Value);

                    GUI.DrawTexture(new Rect(0, 0, minimapRect.width, minimapRect.height), outOfBoundsTexture, ScaleMode.ScaleAndCrop, true);

                    GUI.DrawTexture(new Rect(zx, zy, zw, zh), theMap, ScaleMode.ScaleAndCrop, false);

                    GUI.color = colorSaveTemp;

                    float mapLeft = playerCenterX - mapWidth / 2;
                    float mapTop = playerCenterY + mapHeight / 2;
                    for (int i = chests.Count - 1; i >= 0; i--)
                    {
                        GameObject go = chests[i];
                        try
                        {
                            if (go != null && go.activeSelf)
                            {
                                Vector3 vec = go.transform.position;

                                float chestX = zx + zw * (vec.x - mapLeft) / mapWidth;
                                float chestY = zy + zh * (mapTop - vec.z) / mapHeight;
                                int chestW = 12;
                                int chestH = 10;

                                Texture2D img;
                                var nm = go.name;
                                if (nm.Contains("Ladder", System.StringComparison.InvariantCultureIgnoreCase))
                                {
                                    img = ladder;
                                    chestW = 10;
                                    chestH = 20;
                                }
                                else if (nm.Contains("WreckServer", StringComparison.Ordinal) || nm.StartsWith("Server", StringComparison.Ordinal) || nm.StartsWith("WorldServer", StringComparison.Ordinal))
                                {
                                    img = server;
                                    chestW = 10;
                                    chestH = 15;
                                }
                                else if (nm.Contains("GoldenContainer", StringComparison.Ordinal))
                                {
                                    img = golden;
                                }
                                else if (nm.Contains("WorldContainerStarform", StringComparison.Ordinal))
                                {
                                    img = starform;
                                }
                                else if (nm.Contains("WreckSafe", StringComparison.Ordinal) || nm.Contains("WorldSafe", StringComparison.Ordinal) || nm.StartsWith("Vault", StringComparison.Ordinal))
                                {
                                    img = safe;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else if (nm.Contains("Portal", StringComparison.Ordinal))
                                {
                                    img = portal;
                                    chestW = 10;
                                    chestH = 12;
                                }
                                else if (nm.Contains("Warden", StringComparison.Ordinal))
                                {
                                    img = altar;
                                    chestW = 12;
                                    chestH = 14;
                                }
                                else if (nm.Contains("Stair", StringComparison.Ordinal))
                                {
                                    img = stair;
                                    chestW = 14;
                                    chestH = 14;
                                }
                                else if (nm.Contains("Drone", StringComparison.Ordinal))
                                {
                                    img = drone;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else if (nm.Contains("WreckPilar", StringComparison.Ordinal))
                                {
                                    img = wreck;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else if (IsFurniture(nm))
                                {
                                    img = furniture;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else if (nm.Contains("Poster", StringComparison.Ordinal))
                                {
                                    img = poster;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else if (nm.StartsWith("AnimalEffigie", StringComparison.Ordinal))
                                {
                                    img = animal;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else if (nm.Contains("FusionGenerator", StringComparison.Ordinal))
                                {
                                    img = fusion;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else
                                {
                                    img = chest;
                                }

                                GUI.DrawTexture(new Rect(chestX - chestW / 2, chestY - chestH / 2, chestW, chestH), img, ScaleMode.ScaleAndCrop, true);

                                if (playerY + 1.5 < vec.y)
                                {
                                    GUI.DrawTexture(new Rect(chestX - 5, chestY - chestH / 2 - 8, 10, 6), above, ScaleMode.ScaleAndCrop, true);
                                }

                                if (playerY - 0.5 > vec.y)
                                {
                                    GUI.DrawTexture(new Rect(chestX - 5, chestY + chestH / 2 + 2, 10, 6), below, ScaleMode.ScaleAndCrop, true);
                                }

                                //Logger.LogInfo("Chest " + vec + " rendered at " + chestX + ", " + chestY);
                            }
                            else
                            {
                                chests.RemoveAt(i);
                            }
                        } 
                        catch
                        {
                            chests.RemoveAt(i);
                        }

                    }

                    List<PlayerMainController> players = pm.playersControllers; // [player /*, player, player */];
                    foreach (var controller in players)
                    {
                        if (controller != player)
                        {
                            var vec = controller.transform.position;
                            float chestX = zx + zw * (vec.x - mapLeft) / mapWidth;
                            float chestY = zy + zh * (mapTop - vec.z) / mapHeight;

                            GUI.DrawTexture(new Rect(chestX - 8, chestY - 8, 16, 16), marker2, ScaleMode.ScaleAndCrop, true);

                            var fs = fontSize.Value;
                            if (fs > 0)
                            {
                                var labelStyle = new GUIStyle("label")
                                {
                                    fontSize = fs,
                                    alignment = TextAnchor.UpperCenter,
                                    clipping = TextClipping.Overflow,
                                    wordWrap = false,
                                    fontStyle = FontStyle.Bold
                                };

                                var colorSave = GUI.color;
                                var matrixSave = GUI.matrix;
                                GUI.color = Color.blue;

                                var chest = new Vector2(chestX, chestY);
                                GUI.matrix = unrotatedMatrix;
                                var chestToCenter = chest - mapCenter;
                                var beta = Mathf.Deg2Rad * rotateAround;

                                var x2 = Mathf.Cos(beta) * chestToCenter.x - Mathf.Sin(beta) * chestToCenter.y;
                                var y2 = Mathf.Sin(beta) * chestToCenter.x + Mathf.Cos(beta) * chestToCenter.y;

                                var newPos = new Vector2(mapCenter.x + x2, mapCenter.y + y2);

                                GUI.Label(new Rect(newPos.x, newPos.y + 9, 1, 1), controller.playerName, labelStyle);

                                // restore color and pivot
                                GUI.color = colorSave;
                                GUI.matrix = matrixSave;
                            }
                        }
                    }

                    GUI.EndGroup();

                    GUI.BeginGroup(minimapRect);
                    GUIUtility.RotateAroundPivot(angle, mapCenter);
                    GUI.DrawTexture(new Rect(panelWidth / 2 - 8, panelWidth / 2 - 8, 16, 16), marker, ScaleMode.ScaleAndCrop, true);
                    GUI.EndGroup();
                }
            }
        }

        void UpdateOutOfBoundsTexture()
        {
            if (lastOutOfBoundsColor != outOfBoundsColor.Value)
            {
                lastOutOfBoundsColor = outOfBoundsColor.Value;

                try
                {
                    var parts = lastOutOfBoundsColor.Split(',');
                    var a = int.Parse(parts[0]);
                    var r = int.Parse(parts[1]);
                    var g = int.Parse(parts[2]);
                    var b = int.Parse(parts[3]);

                    var color = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

                    outOfBoundsTexture.SetPixel(0, 0, color);
                    outOfBoundsTexture.Apply();
                }
                catch
                {
                    // we ignore the parsing errors for now
                }
            }
        }

        static Texture2D LoadPNG(string filename)
        {
            var tex = new Texture2D(1000, 1000);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        static AchievementsHandler achievementsHandler;
        static WorldUnitsHandler worldUnitsHandler;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VisualsToggler), nameof(VisualsToggler.ToggleUi))]
        static void VisualsToggler_ToggleUi(List<GameObject> ___uisToHide)
        {
            mapVisible = ___uisToHide[0].activeSelf;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AchievementsHandler), "Start")]
        static void AchievementsHandler_Start(AchievementsHandler __instance)
        {
            achievementsHandler = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldUnitsHandler), nameof(WorldUnitsHandler.CreateUnits))]
        static void WorldUnitsHandler_CreateUnits(WorldUnitsHandler __instance)
        {
            worldUnitsHandler = __instance;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            self.StopAllCoroutines();
            coroutineRunning = false;
            chests.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
            Dictionary<string, Dictionary<string, string>> ___localizationDictionary
        )
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["Minimap_DisableChestAutoScan"] = "Tároló keresés kikapcsolása";
                dict["Minimap_BeginChestAutoScan"] = "Tároló keresés indítása";
                dict["Minimap_StopChestAutoScan"] = "Tároló keresés leállítása, {0} tároló található";
                dict["Minimap_FoundChestAutoScan"] = "{0} tároló található";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["Minimap_DisableChestAutoScan"] = "Disable Chest Auto Scan";
                dict["Minimap_BeginChestAutoScan"] = "Begin Chest Auto Scan";
                dict["Minimap_StopChestAutoScan"] = "Stop Chest Auto Scan, found {0} chest(s)";
                dict["Minimap_FoundChestAutoScan"] = "Found {0} chest(s)";
            }
            if (___localizationDictionary.TryGetValue("russian", out dict))
            {
                dict["Minimap_DisableChestAutoScan"] = "Отключение Автосканирования ящиков";
                dict["Minimap_BeginChestAutoScan"] = "Начать автосканирование ящиков";
                dict["Minimap_StopChestAutoScan"] = "Остановить автосканирование ящиков. Найдено ящиков: {0}";
                dict["Minimap_FoundChestAutoScan"] = "Найдено {0} ящиков";
            }
        }

        static void UpdateKeyBinding()
        {
            if (!toggleXRay.Value.StartsWith("<", StringComparison.Ordinal))
            {
                toggleXRay.Value = "<Keyboard>/" + toggleXRay.Value;
            }

            xRayAction = new InputAction("Toggle XRay Mode", binding: toggleXRay.Value);
            xRayAction.Enable();

            if (!toggleKey.Value.StartsWith("<", StringComparison.Ordinal))
            {
                toggleKey.Value = "<Keyboard>/" + toggleKey.Value;
            }

            mapAction = new InputAction("Toggle minimap", binding: toggleKey.Value);
            mapAction.Enable();
        }
        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBinding();
        }
    }
}
