// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;
using Tessera;
using UnityEngine.InputSystem;
using System;
using Unity.Netcode;
using System.Text;
using LibCommon;

namespace CheatWreckMap
{
    [BepInPlugin(modWreckMapGuid, "(Cheat) Wreck Map", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modWreckMapGuid = "akarnokd.theplanetcraftermods.cheatwreckmap";

        static ManualLogSource logger;

        static GameObject canvas;
        static Text coords;
        static Image mapImage;
        static RectTransform characterRt;

        static Texture2D map;
        const int cellSize = 1;
        const int mapWidthPixels = 30;
        const int mapHeightPixels = 30;

        static readonly Dictionary<int, Dictionary<(int, int), CellType>> cellGrid = [];
        static Color colorFloor = Color.gray;
        static Color colorCurrent = Color.white;
        static Color colorLadder = Color.green;
        static Color colorLadderBottom = new(1f, 0.8f, 0f, 1f);
        static Color colorStair = new(1f, 0f, 0.5f, 1f);
        static Color colorStairBottom = new(0.5f, 0.4f, 0f, 1f);
        static Color colorEmpty = new(0.1f, 0.1f, 0.1f, 0.1f);
        static Color colorBase = Color.yellow;

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> mapVisible;
        static ConfigEntry<bool> debugMode;
        static ConfigEntry<string> baseColor;
        static ConfigEntry<string> emptyColor;
        static ConfigEntry<string> ladderColor;
        static ConfigEntry<string> ladderBottomColor;
        static ConfigEntry<string> stairColor;
        static ConfigEntry<string> stairBottomColor;
        static ConfigEntry<int> renderWidth;
        static ConfigEntry<int> renderHeight;
        static ConfigEntry<int> fontSize;
        static int levelOffset;

        // anything below 200M is considered scene object since 0.9.020
        const int modStorageId = 700_100_000;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            Logger.LogInfo($"Plugin is enabled.");

            modEnabled = Config.Bind("General", "Enabled", true, "Mod is enabled");
            mapVisible = Config.Bind("General", "MapVisible", true, "The map is currently visible");
            debugMode = Config.Bind("General", "DebugMode", false, "Mod is enabled");
            baseColor = Config.Bind("General", "BaseColor", "255,255,255,0", "The basic color of a cell in ARGB values in range 0..255");
            emptyColor = Config.Bind("General", "EmptyColor", "127,25,25,25", "The basic color of emptyness in ARGB values in range 0..255");
            ladderColor = Config.Bind("General", "LadderColor", "255,0,255,0", "The basic color of ladders in ARGB values in range 0..255");
            ladderBottomColor = Config.Bind("General", "LadderBottomColor", "255,255,204,0", "The basic color of ladders in ARGB values in range 0..255");

            stairColor = Config.Bind("General", "StairColor", "255,0,128,0", "The basic color of stairs in ARGB values in range 0..255");
            stairBottomColor = Config.Bind("General", "StairBottomColor", "255,102,128,0", "The basic color of stairs in ARGB values in range 0..255");


            renderWidth = Config.Bind("General", "MapWidth", 750, "The map width in pixels");
            renderHeight = Config.Bind("General", "MapHeight", 750, "The map height in pixels");
            fontSize = Config.Bind("General", "FontSize", 30, "The font size");

            logger = Logger;

            map = new Texture2D(mapWidthPixels, mapHeightPixels)
            {
                filterMode = FilterMode.Point
            };

            OnModConfigChanged(baseColor);

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));
            ModPlanetLoaded.Patch(h, modWreckMapGuid, _ => PlanetLoader_HandleDataAfterLoad());
        }

        public void Update()
        {
            if (!modEnabled.Value)
            {
                return;
            }
            var pm = Managers.GetManager<PlayersManager>();
            if (pm != null)
            {
                var p = pm.GetActivePlayerController();
                if (p != null)
                {
                    var ih = ProceduralInstancesHandler.Instance;
                    if (ih != null && ih.IsReady && ih.IsInsideAnInstance(p.transform.position, false)) 
                    {
                        EnsureCanvas();
                        if (Keyboard.current[Key.L].wasPressedThisFrame 
                            && !Keyboard.current[Key.RightCtrl].isPressed
                            && !Keyboard.current[Key.LeftCtrl].isPressed)
                        {
                            mapVisible.Value = !mapVisible.Value;
                        }
                        if (Keyboard.current[Key.L].wasPressedThisFrame 
                            && (Keyboard.current[Key.RightCtrl].isPressed
                            || Keyboard.current[Key.LeftCtrl].isPressed)
                            )
                        {
                            cellGrid.Clear();
                        }
                        if (Keyboard.current[Key.PageUp].wasPressedThisFrame)
                        {
                            levelOffset--;
                        }
                        if (Keyboard.current[Key.PageDown].wasPressedThisFrame)
                        {
                            levelOffset++;
                        }

                        if (debugMode.Value)
                        {
                            coords.text = "???";
                        }
                        var ray = new Ray(p.transform.position + new Vector3(0, 0.5f, 0), new Vector3(0, -1, 0));
                        var found = false;
                        var insideWreck = false;
                        var all = Physics.RaycastAll(ray, 3);
                        Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
                        foreach (var hit in all)
                        {
                            var tess = hit.transform.GetComponentInParent<TesseraTile>();
                            if (tess != null)
                            {
                                insideWreck = true;
                                // Log(tess);

                                var nm = tess.name;
                                var i1 = nm.IndexOf('(');
                                if (i1 > 0)
                                {
                                    var i2 = nm.IndexOf(")", i1);
                                    if (i2 > 0)
                                    {
                                        var coordsStr = nm[(i1 + 1)..i2];
                                        if (debugMode.Value)
                                        {
                                            coords.text = coordsStr;
                                        }

                                        var coordsParts = coordsStr.Split(',');
                                        if (coordsParts.Length == 3)
                                        {
                                            var cx = int.Parse(coordsParts[0]);
                                            var cy = int.Parse(coordsParts[1]);
                                            var cz = int.Parse(coordsParts[2]);

                                            if (!debugMode.Value)
                                            {
                                                var cs = "<b><i>Wreck Level " + cy + "</i></b>";
                                                if (levelOffset != 0)
                                                {
                                                    cs += " - <color=#A0A0FF>Showing level " + (cy + levelOffset) + "</color>";
                                                }
                                                coords.text = cs;
                                            }

                                            ApplyOffsets(cx, cy, cz, tess);

                                            if (mapVisible.Value)
                                            {
                                                UpdateMapTexture(cx, cy, cz);
                                            }
                                            found = true;


                                            var parentGo = tess.transform;
                                            while (parentGo.parent != null)
                                            {
                                                parentGo = parentGo.parent;
                                            }

                                            var ea = characterRt.transform.eulerAngles;

                                            var angle = p.transform.eulerAngles.y;
                                            angle -= parentGo.transform.eulerAngles.y;

                                            characterRt.transform.eulerAngles = new Vector3(ea.x, ea.y, -angle);

                                            var pxx = -renderWidth.Value / 2 + (cx + 0.5f) * renderWidth.Value / map.width;
                                            var pyy = -renderHeight.Value / 2 + (cz + 0.5f) * renderHeight.Value / map.height;

                                            characterRt.transform.localPosition = new Vector2(pxx, pyy);
                                            characterRt.gameObject.SetActive(true);
                                        }
                                    }
                                }
                                break;
                            }
                        }
                        if (!found && mapVisible.Value)
                        {
                            UpdateMapTexture(-1, -1, -1);
                            characterRt.gameObject.SetActive(false);
                        }
                        canvas.SetActive(mapVisible.Value && !(Managers.GetManager<WindowsHandler>()?.GetHasUiOpen() ?? false) && insideWreck);

                    }
                    else
                    {
                        Destroy(canvas);
                        canvas = null;
                        levelOffset = 0;
                    }
                }
            }
        }

        void ApplyOffsets(int cx, int cy, int cz, TesseraTile tess)
        {
            var rot = tess.GetComponent<Transform>()?.localEulerAngles.y ?? 0f;
            foreach (var offset in tess.sylvesOffsets)
            {
                var iy = cy + offset.y;
                if (!cellGrid.TryGetValue(iy, out var cgr))
                {
                    cgr = [];
                    cellGrid[iy] = cgr;
                }

                var ct = CellType.Corridor;
                if (tess.name.Contains("TileLadder"))
                {
                    if (offset.y == 0)
                    {
                        ct = CellType.Ladder;
                    }
                    else
                    {
                        ct = CellType.Ladder_Bottom;
                    }
                }
                else if (tess.name.Contains("Stair"))
                {
                    if (offset.y == 0)
                    {
                        ct = CellType.Stair;
                    }
                    else
                    {
                        ct = CellType.Stair_Bottom;
                    }
                }

                var key = (cx + offset.x, cz + offset.z);
                if (rot > 89f && rot < 91f)
                {
                    key = (cx + offset.z, cz - offset.x);
                }
                else if (rot > 179f && rot < 181f)
                {
                    key = (cx - offset.x, cz - offset.z);
                }
                else if (rot > 269f && rot < 271f)
                {
                    key = (cx - offset.z, cz + offset.x);
                }

                if (cgr.TryGetValue(key, out var gt))
                {
                    if (gt != ct)
                    {
                        cgr[key] = ct;
                    }
                }
                else
                {
                    cgr[key] = ct;
                }
            }
        }

        void UpdateMapTexture(int px, int py, int pz)
        {
            py += levelOffset;
            if (map.width <= px || map.height <= pz)
            {
                var newSize = Math.Max(px + 1, pz + 1);
                map.width = newSize; 
                map.height = newSize;
            }
            cellGrid.TryGetValue(py, out var cgv);
            for (int x = 0; x < map.width; x++)
            {
                var cx = x / cellSize;
                for (int y = 0; y < map.height; y++)
                {
                    var cy = y / cellSize;

                    if (cgv != null)
                    {
                        if (cgv.TryGetValue((cx, cy), out var ct))
                        {
                            if (cx == px && cy == pz)
                            {
                                map.SetPixel(x, y, colorCurrent);
                            }
                            else
                            {
                                if (ct == CellType.Ladder)
                                {
                                    map.SetPixel(x, y, colorLadder);
                                }
                                else if (ct == CellType.Ladder_Bottom)
                                {
                                    map.SetPixel(x, y, colorLadderBottom);
                                }
                                else if (ct == CellType.Stair)
                                {
                                    map.SetPixel(x, y, colorStair);
                                }
                                else if (ct == CellType.Stair_Bottom)
                                {
                                    map.SetPixel(x, y, colorStairBottom);
                                }
                                else
                                {
                                    map.SetPixel(x, y, colorFloor);
                                }
                            }
                        }
                        else
                        {
                            map.SetPixel(x, y, colorEmpty);
                        }
                    }
                    else
                    {
                        map.SetPixel(x, y, colorEmpty);
                    }
                }
            }
            map.Apply();
            mapImage.sprite = Sprite.Create(map, new Rect(0, 0, map.width, map.height), new Vector2(0.5f, 0.5f));
        }

        void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = new GameObject("CheatWreckMapCanvas");
            var cv = canvas.AddComponent<Canvas>();
            cv.sortingOrder = 500;
            cv.renderMode = RenderMode.ScreenSpaceOverlay;

            var textGo = new GameObject("PositionText");
            textGo.transform.SetParent(cv.transform, false);

            coords = textGo.AddComponent<Text>();

            coords.fontSize = fontSize.Value;
            coords.color = Color.white;
            coords.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            coords.horizontalOverflow = HorizontalWrapMode.Overflow;
            coords.verticalOverflow = VerticalWrapMode.Overflow;
            coords.alignment = TextAnchor.MiddleCenter;

            var rt = textGo.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(0, - renderHeight.Value / 2 - coords.preferredHeight / 2, 0);

            var imageGo = new GameObject("MapImage");
            imageGo.transform.SetParent (cv.transform, false);

            mapImage = imageGo.AddComponent<Image>();
            mapImage.color = colorBase;

            rt = imageGo.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(0, 0, 0);
            rt.sizeDelta = new Vector2(renderWidth.Value, renderHeight.Value);

            var characterGo = new GameObject("Character");
            characterGo.transform.SetParent(cv.transform, false);
            var characterOutline = characterGo.AddComponent<Outline>();
            characterOutline.effectDistance = new Vector2(2, 2);
            characterOutline.effectColor = Color.green;


            var characterText = characterGo.AddComponent<Text>();

            characterText.fontSize = fontSize.Value;
            characterText.color = Color.blue;
            characterText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            characterText.horizontalOverflow = HorizontalWrapMode.Overflow;
            characterText.verticalOverflow = VerticalWrapMode.Overflow;
            characterText.text = "↑";
            characterText.fontStyle = FontStyle.Bold;
            characterText.alignment = TextAnchor.MiddleCenter;
            characterRt = characterGo.GetComponent<RectTransform>();

            canvas.SetActive(false);
        }

        static WorldObject EnsureStorage()
        {
            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(modStorageId);
            wo ??= WorldObjectsHandler.Instance.CreateNewWorldObject(
                GroupsHandler.GetGroupViaId("Iron"), modStorageId, null, true);
            return wo;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void Patch_UiWindowPause_OnQuit()
        {
            cellGrid.Clear();
            levelOffset = 0;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            Patch_UiWindowPause_OnQuit();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SavedDataHandler), nameof(SavedDataHandler.SaveWorldData))]
        static void SaveMap()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                var wo = EnsureStorage();

                WorldInstanceHandler wih = Managers.GetManager<WorldInstanceHandler>();

                var ow = wih?.GetOpenedWorldInstanceData();
                var sb = new StringBuilder(1024);
                sb.Append(ow?.GetSeed() ?? 0);

                foreach (var kv in cellGrid)
                {
                    foreach (var e in kv.Value)
                    {
                        sb.Append(';');
                        sb.Append(kv.Key).Append(',')
                            .Append(e.Key.Item1).Append(',')
                            .Append(e.Key.Item2).Append(',')
                            .Append((int)e.Value)
                        ;
                    }
                }
                var str = sb.ToString();
                wo.SetText(str);
                Log("Map saved: " + str);
            }
        }

        static void PlanetLoader_HandleDataAfterLoad()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                var wo = EnsureStorage();

                var txt = wo.GetText() ?? "";
                Log("Found procedural wreck map: " + txt);
                var sections = txt.Split(';');

                WorldInstanceHandler wih = Managers.GetManager<WorldInstanceHandler>();

                var ow = wih?.GetOpenedWorldInstanceData();
                if ((ow != null && txt.Length != 0 && ow.GetSeed().ToString() == sections[0])
                    || (txt.Length != 0 && sections[0] == "0")
                )
                {
                    Log("Loading procedural wreck map: " + txt);
                    cellGrid.Clear();

                    for (int i = 1; i < sections.Length; i++)
                    {
                        string sec = sections[i];
                        var parts = sec.Split(',');
                        var cy = int.Parse(parts[0]);
                        var cx = int.Parse(parts[1]);
                        var cz = int.Parse(parts[2]);
                        var ct = CellType.Corridor;
                        if (parts.Length > 3)
                        {
                            ct = (CellType)int.Parse(parts[3]);
                        }

                        if (!cellGrid.TryGetValue(cy, out var cgr))
                        {
                            cgr = [];
                            cellGrid[cy] = cgr;
                        }
                        cgr[(cx, cz)] = ct;
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldInstanceHandler), nameof(WorldInstanceHandler.UnloadInstance))]
        static void WorldInstanceHandler_UnloadInstance()
        {
            // cellGrid.Clear();
        }

        static void Log(object message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        static void ParseColor(ConfigEntry<string> cfge, ref Color color)
        {
            var str = cfge.Value;
            var parts = str.Split(',');
            if (parts.Length == 4)
            {
                try
                {
                    color = new(
                        int.Parse(parts[1]) / 255f,
                        int.Parse(parts[2]) / 255f,
                        int.Parse(parts[3]) / 255f,
                        int.Parse(parts[0]) / 255f
                    );
                } catch
                {
                    // ignored
                }
            }
        }

        static void OnModConfigChanged(ConfigEntryBase _)
        {
            ParseColor(baseColor, ref colorBase);
            ParseColor(emptyColor, ref colorEmpty);
            ParseColor(ladderColor, ref colorLadder);
            ParseColor(ladderBottomColor, ref colorLadderBottom);
            ParseColor(stairColor, ref colorStair);
            ParseColor(stairBottomColor, ref colorStairBottom);
            Destroy(canvas);
            canvas = null;
        }

        /*

        [HarmonyPostfix]
        [HarmonyPatch(typeof(TesseraGenerator), nameof(TesseraGenerator.Instantiate), [typeof(TesseraTileInstance), typeof(Transform), typeof(IEngineInterface)])]
        static void TesseraGenerator_Instantiate(TesseraTileInstance instance, ref GameObject[] __result)
        {
            foreach(var go in __result)
            {
                var gridref = go.AddComponent<GridRef>();
                gridref.cell = instance.Cell;
            }
        }

        internal class GridRef : MonoBehaviour
        {
            internal Vector3Int cell;
        }
        */
        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(TesseraTile))]
        [HarmonyPatch(MethodType.Constructor)]
        static void TesseraTile_Constructor(TesseraTile __instance)
        {
            if (__instance.name.Contains("Destination"))
            {
                logger.LogInfo(Environment.StackTrace);
            }
        }
        */

        public enum CellType
        {
            Empty,
            Corridor,
            Ladder,
            Ladder_Bottom,
            Stair,
            Stair_Bottom
        }
    }
}
