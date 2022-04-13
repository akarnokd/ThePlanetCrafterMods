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
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatminimap", "(Cheat) Minimap", "1.0.0.4")]
    public class Plugin : BaseUnityPlugin
    {
        Texture2D barren;
        Texture2D lush;
        Texture2D marker;

        ConfigEntry<int> mapSize;
        ConfigEntry<int> zoomLevel;
        ConfigEntry<string> toggleKey;
        ConfigEntry<int> zoomInMouseButton;
        ConfigEntry<int> zoomOutMouseButton;

        static bool mapVisible = true;
        static bool mapManualVisible = true;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            barren = LoadPNG(Path.Combine(dir, "map_barren.png"));
            lush = LoadPNG(Path.Combine(dir, "map_lush.png"));
            marker = LoadPNG(Path.Combine(dir, "player_marker.png"));

            mapSize = Config.Bind("General", "MapSize", 400, "The minimap panel size");
            zoomLevel = Config.Bind("General", "ZoomLevel", 4, "The zoom level");
            toggleKey = Config.Bind("General", "ToggleKey", "N", "The key to press to toggle the minimap");
            zoomInMouseButton = Config.Bind("General", "ZoomInMouseButton", 4, "Which mouse button to use for zooming in (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");
            zoomOutMouseButton = Config.Bind("General", "ZoomOutMouseButton", 5, "Which mouse button to use for zooming out (0-none, 1-left, 2-right, 3-middle, 4-forward, 5-back)");

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

        void Update()
        {
            PlayersManager pm = Managers.GetManager<PlayersManager>();
            PlayerMainController player = pm?.GetActivePlayerController();
            WindowsHandler wh = Managers.GetManager<WindowsHandler>();
            if (player != null && wh != null && !wh.GetHasUiOpen())
            {
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
                    {
                        mapManualVisible = !mapManualVisible;
                    }
                }
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
                    Rect minimapRect = new Rect(0, Screen.height - panelWidth - 350, panelWidth, panelWidth);
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
                    float playerCenterX = 722;
                    float playerCenterY = 728;
                    float mapWidth = 3232;
                    float mapHeight = 4240;

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
                    float zx = 0;
                    float zy = 0;
                    float zw = zoom * shrink * mapImageWidth;
                    float zh = zoom * shrink * mapImageHeight;

                    // center on the player
                    zx = playerExcentricX * shrink * zoom - zw / 2 + panelWidth / 2;
                    zy = playerExcentricY * shrink * zoom - zh / 2 + panelWidth / 2;

                    GUI.BeginGroup(minimapRect);
                    GUIUtility.RotateAroundPivot(-angle, mapCenter);
                    GUI.DrawTexture(new Rect(zx, zy, zw, zh), theMap, ScaleMode.ScaleAndCrop, false);
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
    }
}
