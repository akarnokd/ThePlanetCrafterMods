using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.InputSystem.Controls;

namespace CheatMinimap
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatminimap", "(Cheat) Minimap", "1.0.0.11")]
    public class Plugin : BaseUnityPlugin
    {
        Texture2D barren;
        Texture2D lush;
        Texture2D marker;
        Texture2D chest;
        Texture2D golden;

        ConfigEntry<int> mapSize;
        ConfigEntry<int> mapBottom;
        ConfigEntry<int> zoomLevel;
        ConfigEntry<string> toggleKey;
        ConfigEntry<int> zoomInMouseButton;
        ConfigEntry<int> zoomOutMouseButton;
        ConfigEntry<int> autoScanForChests;
        ConfigEntry<int> fixedRotation;

        static bool mapVisible = true;
        static bool mapManualVisible = true;
        static int autoScanEnabled = 0;
        static bool coroutineRunning = false;

        static Plugin self;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            barren = LoadPNG(Path.Combine(dir, "map_barren.png"));
            lush = LoadPNG(Path.Combine(dir, "map_lush.png"));
            marker = LoadPNG(Path.Combine(dir, "player_marker.png"));
            chest = LoadPNG(Path.Combine(dir, "chest.png"));
            golden = LoadPNG(Path.Combine(dir, "chest_golden.png"));

            mapSize = Config.Bind("General", "MapSize", 400, "The minimap panel size");
            mapBottom = Config.Bind("General", "MapBottom", 350, "Panel position from the bottom of the screen");
            zoomLevel = Config.Bind("General", "ZoomLevel", 4, "The zoom level");
            toggleKey = Config.Bind("General", "ToggleKey", "N", "The key to press to toggle the minimap");
            zoomInMouseButton = Config.Bind("General", "ZoomInMouseButton", 4, "Which mouse button to use for zooming in (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");
            zoomOutMouseButton = Config.Bind("General", "ZoomOutMouseButton", 5, "Which mouse button to use for zooming out (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");
            autoScanForChests = Config.Bind("General", "AutoScanForChests", 5, "If nonzero and the minimap is visible, the minimap periodically scans for chests every N seconds. Toggle with Alt+N");
            fixedRotation = Config.Bind("General", "FixedRotation", -1, "If negative, the map rotates on screen. If Positive, the map is fixed to that rotation in degrees (0..360).");

            self = this;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static ButtonControl MouseButtonForIndex(int index)
        {
            switch (index)
            {
                case 1: return Mouse.current.leftButton;
                case 2: return Mouse.current.rightButton;
                case 3: return Mouse.current.middleButton;
                case 4: return Mouse.current.forwardButton;
                case 5: return Mouse.current.backButton;
                default: return null;
            }
        }

        static List<GameObject> chests = new List<GameObject>();

        IEnumerator AutoScan()
        {
            for (; ; )
            {
                int n = autoScanForChests.Value;
                if (mapManualVisible && n > 0 && autoScanEnabled == 1)
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
                PropertyInfo pi = typeof(Key).GetProperty(toggleKey.Value.ToString().ToUpper());
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
                        mapManualVisible = !mapManualVisible;
                    }
                }
            }
        }

        void FindChests()
        {
            chests.Clear();

            foreach (GameObject ia in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                try
                {
                    if (ia.name.Contains("WorldContainer") || ia.name.Contains("GoldenContainer") || ia.name.Contains("WorldCanister"))
                    {
                        chests.Add(ia);
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
            if (mapVisible && mapManualVisible)
            {
                PlayersManager pm = Managers.GetManager<PlayersManager>();
                PlayerMainController player = pm?.GetActivePlayerController();
                WindowsHandler wh = Managers.GetManager<WindowsHandler>();
                if (player != null && wh != null && !wh.GetHasUiOpen())
                {
                    int panelWidth = mapSize.Value;

                    float angle = player.transform.eulerAngles.y;
                    Rect minimapRect = new Rect(0, Screen.height - panelWidth - mapBottom.Value, panelWidth, panelWidth);
                    Vector2 mapCenter = new Vector2(panelWidth / 2, panelWidth / 2);
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
                    float playerCenterX = 700;
                    float playerCenterY = 800;
                    float mapWidth = 3400;
                    float mapHeight = 4400;

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

                    if (fixRot >= 0)
                    {
                        GUIUtility.RotateAroundPivot(fixedAngle, mapCenter);
                    }
                    else
                    {
                        GUIUtility.RotateAroundPivot(-angle, mapCenter);
                    }
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
            Texture2D tex = new Texture2D(750, 1000);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        static AchievementsHandler achievementsHandler;
        static WorldUnitsHandler worldUnitsHandler;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LiveDevTools), nameof(LiveDevTools.ToggleUi))]
        static void LiveDevTools_ToggleUi(List<GameObject> ___handObjectsToHide)
        {
            mapVisible = !___handObjectsToHide[0].activeSelf;
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
        static void UiWindowPause_OnQuit(UiWindowPause __instance)
        {
            self.StopAllCoroutines();
            coroutineRunning = false;
            chests.Clear();
        }
    }
}
