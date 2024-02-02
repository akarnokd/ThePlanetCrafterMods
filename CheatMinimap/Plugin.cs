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

namespace CheatMinimap
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatminimap", "(Cheat) Minimap", PluginInfo.PLUGIN_VERSION)]
    public partial class Plugin : BaseUnityPlugin
    {
        Texture2D barren;
        Texture2D lush;
        Texture2D marker;
        Texture2D marker2;
        Texture2D chest;
        Texture2D golden;

        ConfigEntry<int> mapSize;
        ConfigEntry<int> mapBottom;
        ConfigEntry<int> mapPanelLeft;
        ConfigEntry<int> zoomLevel;
        ConfigEntry<string> toggleKey;
        ConfigEntry<int> zoomInMouseButton;
        ConfigEntry<int> zoomOutMouseButton;
        ConfigEntry<int> autoScanForChests;
        ConfigEntry<int> fixedRotation;
        static ConfigEntry<bool> mapManualVisible;
        static ConfigEntry<int> fontSize;

        static bool mapVisible = true;
        static int autoScanEnabled = 0;
        static bool coroutineRunning = false;
        static ConfigEntry<bool> photographMap;

        static Plugin self;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            barren = LoadPNG(Path.Combine(dir, "map_barren.png"));
            lush = LoadPNG(Path.Combine(dir, "map_lush.png"));
            marker = LoadPNG(Path.Combine(dir, "player_marker.png"));
            marker2 = LoadPNG(Path.Combine(dir, "player_marker_2.png"));
            chest = LoadPNG(Path.Combine(dir, "chest.png"));
            golden = LoadPNG(Path.Combine(dir, "chest_golden.png"));

            mapSize = Config.Bind("General", "MapSize", 400, "The minimap panel size");
            mapBottom = Config.Bind("General", "MapBottom", 350, "Panel position from the bottom of the screen");
            mapPanelLeft = Config.Bind("General", "MapLeft", 0, "Panel position from the left of the screen");
            zoomLevel = Config.Bind("General", "ZoomLevel", 4, "The zoom level");
            toggleKey = Config.Bind("General", "ToggleKey", "N", "The key to press to toggle the minimap");
            zoomInMouseButton = Config.Bind("General", "ZoomInMouseButton", 4, "Which mouse button to use for zooming in (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");
            zoomOutMouseButton = Config.Bind("General", "ZoomOutMouseButton", 5, "Which mouse button to use for zooming out (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");
            autoScanForChests = Config.Bind("General", "AutoScanForChests", 5, "If nonzero and the minimap is visible, the minimap periodically scans for chests every N seconds. Toggle with Alt+N");
            fixedRotation = Config.Bind("General", "FixedRotation", -1, "If negative, the map rotates on screen. If Positive, the map is fixed to that rotation in degrees (0..360).");
            photographMap = Config.Bind("General", "PhotographMap", false, "Not meant for end-users. (Photographs the map when pressing U for development purposes.)");
            mapManualVisible = Config.Bind("General", "MapVisible", true, "Should the map be visible?");
            fontSize = Config.Bind("General", "FontSize", 16, "The size of the names of other players, use 0 to disable showing their name.");

            self = this;

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
                    zoomLevel.Value = Mathf.Clamp(zoomLevel.Value + 1, 1, 10);
                }
                if (MouseButtonForIndex(zoomOutMouseButton.Value)?.wasPressedThisFrame ?? false)
                {
                    zoomLevel.Value = Mathf.Clamp(zoomLevel.Value - 1, 1, 10);
                }

                if (Keyboard.current[k].wasPressedThisFrame)
                {
                    if (Keyboard.current[Key.LeftShift].isPressed)
                    {
                        zoomLevel.Value = Mathf.Clamp(zoomLevel.Value + 1, 1, 10);
                    }
                    else
                    if (Keyboard.current[Key.LeftCtrl].isPressed)
                    {
                        zoomLevel.Value = Mathf.Clamp(zoomLevel.Value - 1, 1, 10);
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
                    if (go.name.Contains("WorldContainer") || go.name.Contains("GoldenContainer") || go.name.Contains("WorldCanister"))
                    {
                        chests.Add(go);
                    }
                }
                catch
                {
                    // some kind of despawn?
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
                    int panelWidth = mapSize.Value;

                    float angle = player.transform.eulerAngles.y;
                    var minimapRect = new Rect(mapPanelLeft.Value, Screen.height - panelWidth - mapBottom.Value, panelWidth, panelWidth);
                    var mapCenter = new Vector2(panelWidth / 2, panelWidth / 2);
                    float zoom = zoomLevel.Value;

                    Texture2D theMap = barren;

                    
                    if (achievementsHandler != null && worldUnitsHandler != null)
                    {
                        float currT = worldUnitsHandler.GetUnit(DataConfig.WorldUnitType.Terraformation).GetValue();
                        float minT = achievementsHandler.stageMoss.GetStageStartValue();
                        if (currT >= minT)
                        {
                            theMap = lush;
                        }
                    }

                    float mapImageWidth = theMap.width;
                    float mapImageHeight = theMap.height;

                    // calibrated to the given map
                    float playerCenterX = 400;
                    float playerCenterY = 800;
                    float mapWidth = 4000;
                    float mapHeight = 4000;

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

                    GUI.DrawTexture(new Rect(zx, zy, zw, zh), theMap, ScaleMode.ScaleAndCrop, false);
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

                                Texture2D img;
                                if (go.name.Contains("GoldenContainer"))
                                {
                                    img = golden;
                                }
                                else
                                {
                                    img = chest;
                                }

                                GUI.DrawTexture(new Rect(chestX - 6, chestY - 5, 12, 10), img, ScaleMode.ScaleAndCrop, true);
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

        static Texture2D LoadPNG(string filename)
        {
            var tex = new Texture2D(750, 1000);
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
    }
}
