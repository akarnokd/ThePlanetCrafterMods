// Copyright (c) 2022-2024, David Karnok & Contributors
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

namespace CheatMinimap
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatminimap", "(Cheat) Minimap", PluginInfo.PLUGIN_VERSION)]
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
        Texture2D humbleBarren;
        Texture2D humbleLush;

        ConfigEntry<int> mapSize;
        ConfigEntry<int> mapBottom;
        ConfigEntry<int> mapPanelLeft;
        ConfigEntry<int> zoomLevel;
        ConfigEntry<int> maxZoomLevel;
        ConfigEntry<string> toggleKey;
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

        static ConfigEntry<bool> mapManualVisible;
        static ConfigEntry<int> fontSize;

        static ConfigEntry<string> outOfBoundsColor;
        static string lastOutOfBoundsColor;

        static bool mapVisible = true;
        static int autoScanEnabled = 0;
        static bool coroutineRunning = false;
        static ConfigEntry<bool> photographMap;

        static Plugin self;

        const int primeMapMinX = -2000;
        const int primeMapMinY = -2000;
        const int primeMapMaxX = 3000;
        const int primeMapMaxY = 3000;

        const int humbleMapMinY = -2300;
        const int humbleMapMinX = -2300;
        const int humbleMapMaxY = 2700;
        const int humbleMapMaxX = 2700;


        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

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
            photographMap = Config.Bind("General", "PhotographMap", false, "Not meant for end-users. (Photographs the map when pressing U for development purposes.)");
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

            self = this;

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
            if (player != null && wh != null && !wh.GetHasUiOpen())
            {
                if (!coroutineRunning)
                {
                    coroutineRunning = true;
                    StartCoroutine(AutoScan());
                }
                FieldInfo pi = typeof(Key).GetField(toggleKey.Value.ToString().ToUpper());
                Key k = Key.N;
                if (pi != null)
                {
                    k = (Key)pi.GetRawConstantValue();
                }

                if (MouseButtonForIndex(zoomInMouseButton.Value)?.wasPressedThisFrame ?? false)
                {
                    zoomLevel.Value = Mathf.Clamp(zoomLevel.Value + 1, 1, maxZoomLevel.Value);
                }
                if (MouseButtonForIndex(zoomOutMouseButton.Value)?.wasPressedThisFrame ?? false)
                {
                    zoomLevel.Value = Mathf.Clamp(zoomLevel.Value - 1, 1, maxZoomLevel.Value);
                }

                if (Keyboard.current[k].wasPressedThisFrame)
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
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Disabling Chest AutoScan");
                            }
                            else
                            if (autoScanEnabled == 1)
                            {
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Begin Chest AutoScan");
                                FindChests();
                            }
                            else
                            {
                                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Stop Chest AutoScan, Chests found: " + chests.Count);
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
            UpdatePhoto();
        }

        void FindChests()
        {
            chests.Clear();

            foreach (ActionOpenable ia in FindObjectsByType<ActionOpenable>(FindObjectsSortMode.None))
            {
                try
                {
                    var go = ia.gameObject;
                    if (go.name.Contains("WorldContainer") 
                        || go.name.Contains("GoldenContainer") 
                        || go.name.Contains("WorldCanister")
                        || go.name.Contains("WreckContainer")
                        || go.name.Contains("WreckCanister")
                    )
                    {
                        chests.Add(go);
                    }
                    else if (
                        ((go.name.Contains("WreckSafe") || go.name.Contains("WorldSafe")) && showSafes.Value)
                        || (go.name.Contains("Warden") && showAltars.Value)
                        || (showDrones.Value && (!go.name.Contains("Clone") && go.name.StartsWith("Drone") && go.name.Length > 5))
                        || go.name.Contains("WorldWardrobe")
                    )
                    {
                        var invAssoc = go.GetComponentInParent<InventoryAssociated>();
                        var invAssocProxy = go.GetComponentInParent<InventoryAssociatedProxy>();

                        if (invAssoc != null && (invAssocProxy == null || (NetworkManager.Singleton?.IsServer ?? false)))
                        {
                            var id = invAssoc.GetInventoryId();
                            if (id > 0)
                            {
                                var inv = InventoriesHandler.Instance.GetInventoryById(id);
                                if (inv != null && inv.GetInsideWorldObjects().Count != 0)
                                {
                                    chests.Add(go);
                                }
                            }
                            else if (go.GetComponentInParent<InventoryFromScene>() != null && id < 0)
                            {
                                if (go.TryGetComponent<WorldUniqueId>(out var wuid))
                                {
                                    var inv = InventoriesHandler.Instance.GetInventoryById(wuid.GetWorldUniqueId());
                                    if (inv == null || inv.GetInsideWorldObjects().Count != 0)
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
                                if (inv != null && inv.GetInsideWorldObjects().Count != 0)
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
                        && am.transform.parent.parent.gameObject.name.Contains("LadderWreck"))
                    {
                        var go = am.transform.parent.parent.gameObject;
                        if (ladderSet.Add(go))
                        {
                            chests.Add(go);
                        }
                    }
                    if (am != null && am.gameObject.name.Contains("ladder_"))
                    {
                        var go = am.transform.parent.gameObject;
                        if (ladderSet.Add(go))
                        {
                            chests.Add(go);
                        }
                    }
                }
            }
            if (showServers.Value)
            {
                foreach (var id in FindObjectsByType<ActionDeconstructible>(FindObjectsSortMode.None))
                {
                    if (id.transform.parent != null && id.transform.parent.name.Contains("WreckServer"))
                    {
                        chests.Add(id.transform.parent.gameObject);
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
                    if (id.name.Contains("Stair"))
                    {
                        chests.Add(id.transform.gameObject);
                    }
                }
            }

            if (autoScanEnabled == 0)
            {
                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Found " + chests.Count + " chests");
            }
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

                    // calibrated to the given map
                    float playerCenterX = (primeMapMaxX + primeMapMinX) / 2;
                    float playerCenterY = (primeMapMaxY + primeMapMinY) / 2;
                    float mapWidth = primeMapMaxX - primeMapMinX;
                    float mapHeight = primeMapMaxY - primeMapMinY;

                    var pd = Managers.GetManager<PlanetLoader>()?.GetPlanetData();

                    var isPrime = pd != null && (pd.id == "" || pd.id == "Prime");
                    if (isPrime)
                    {
                        if (achievementsHandler != null && worldUnitsHandler != null)
                        {
                            float currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                            float minT = achievementsHandler.stageMoss.GetStageStartValue();
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
                        playerCenterX = (humbleMapMaxX + humbleMapMinX) / 2;
                        playerCenterY = (humbleMapMaxY + humbleMapMinY) / 2;
                        mapWidth = humbleMapMaxX - humbleMapMinX;
                        mapHeight = humbleMapMaxY - humbleMapMinY;
                        theMap = humbleBarren;

                        if (achievementsHandler != null && worldUnitsHandler != null)
                        {
                            float currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                            float minT = achievementsHandler.stageMoss.GetStageStartValue();
                            if (currT >= minT)
                            {
                                theMap = humbleLush;
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
                    foreach (GameObject go in new List<GameObject>(chests))
                    {
                        
                        try
                        {
                            if (go.activeSelf)
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
                                else if (nm.Contains("WreckServer"))
                                {
                                    img = server;
                                    chestW = 10;
                                    chestH = 15;
                                }
                                else if (nm.Contains("GoldenContainer"))
                                {
                                    img = golden;
                                }
                                else if (nm.Contains("WorldContainerStarform"))
                                {
                                    img = starform;
                                }
                                else if (nm.Contains("WreckSafe") || nm.Contains("WorldSafe"))
                                {
                                    img = safe;
                                    chestW = 16;
                                    chestH = 16;
                                }
                                else if (nm.Contains("Portal"))
                                {
                                    img = portal;
                                    chestW = 10;
                                    chestH = 12;
                                }
                                else if (nm.Contains("Warden"))
                                {
                                    img = altar;
                                    chestW = 12;
                                    chestH = 14;
                                }
                                else if (nm.Contains("Stair"))
                                {
                                    img = stair;
                                    chestW = 14;
                                    chestH = 14;
                                }
                                else if (nm.Contains("Drone"))
                                {
                                    img = drone;
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
                                chests.Remove(go);
                            }
                        } 
                        catch
                        {
                            chests.Remove(go);
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

    }
}
