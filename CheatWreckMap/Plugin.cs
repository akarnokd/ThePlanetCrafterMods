// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Tessera;
using UnityEngine.InputSystem;
using System;

namespace CheatWreckMap
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatwreckmap", "(Cheat) Wreck Map", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static GameObject canvas;
        static Text coords;
        static Image mapImage;

        static Texture2D map;
        const int cellSize = 1;
        const int mapWidthPixels = 30;
        const int mapHeightPixels = 30;
        const int renderWidth = 750;
        const int renderHeight = 750;

        static readonly Dictionary<int, HashSet<(int, int)>> cellGrid = [];
        static Color colorFloor = Color.gray;
        static Color colorCurrent = Color.white;

        static ConfigEntry<bool> modEnabled;
        static ConfigEntry<bool> mapVisible;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            Logger.LogInfo($"Plugin is enabled.");

            modEnabled = Config.Bind("General", "Enabled", true, "Mod is enabled");
            mapVisible = Config.Bind("General", "MapVisible", true, "The map is currently visible");

            logger = Logger;

            map = new Texture2D(mapWidthPixels, mapHeightPixels)
            {
                filterMode = FilterMode.Point
            };

            Harmony.CreateAndPatchAll(typeof(Plugin));
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
                    if (ih != null && ih.IsReady && ih.IsInsideAnInstance(p.transform.position)) 
                    {
                        EnsureCanvas();
                        if (Keyboard.current[Key.L].wasPressedThisFrame)
                        {
                            mapVisible.Value = !mapVisible.Value;
                        }
                        canvas.SetActive(mapVisible.Value);

                        coords.text = "???";
                        var ray = new Ray(p.transform.position + new Vector3(0, 0.5f, 0), new Vector3(0, -1, 0));
                        var found = false;
                        var all = Physics.RaycastAll(ray, 3);
                        Array.Sort(all, (a, b) => a.distance.CompareTo(b.distance));
                        foreach (var hit in all)
                        {
                            var tess = hit.transform.GetComponentInParent<TesseraTile>();
                            if (tess != null)
                            {
                                logger.LogInfo(tess);

                                var nm = tess.name;
                                var i1 = nm.IndexOf('(');
                                if (i1 > 0)
                                {
                                    var i2 = nm.IndexOf(")", i1);
                                    if (i2 > 0)
                                    {
                                        var coordsStr = nm[(i1 + 1)..i2];
                                        coords.text = coordsStr;

                                        var coordsParts = coordsStr.Split(',');
                                        if (coordsParts.Length == 3)
                                        {
                                            var cx = int.Parse(coordsParts[0]);
                                            var cy = int.Parse(coordsParts[1]);
                                            var cz = int.Parse(coordsParts[2]);

                                            if (!cellGrid.TryGetValue(cy, out var cgr))
                                            {
                                                cgr = [];
                                                cellGrid[cy] = cgr;
                                            }

                                            foreach (var offset in tess.sylvesOffsets)
                                            {

                                                cgr.Add((cx + offset.x, cz + offset.z));
                                            }

                                            if (mapVisible.Value)
                                            {
                                                UpdateMapTexture(cx, cy, cz);
                                            }
                                            found = true;
                                        }
                                    }
                                }
                                break;
                            }
                        }
                        if (!found && mapVisible.Value)
                        {
                            UpdateMapTexture(-1, -1, -1);
                        }

                    }
                    else
                    {
                        Destroy(canvas);
                        canvas = null;
                    }
                }
            }
        }

        void UpdateMapTexture(int px, int py, int pz)
        {
            cellGrid.TryGetValue(py, out var cgv);
            for (int x = 0; x < mapWidthPixels; x++)
            {
                var cx = x / cellSize;
                for (int y = 0; y < mapHeightPixels; y++)
                {
                    var cy = y / cellSize;

                    if (cgv != null)
                    {
                        if (cgv.Contains((cx, cy)))
                        {
                            if (cx == px && cy == pz)
                            {
                                map.SetPixel(x, y, colorCurrent);
                            }
                            else
                            {
                                map.SetPixel(x, y, colorFloor);
                            }
                        }
                        else
                        {
                            map.SetPixel(x, y, new Color(0, 0, 0, 0));
                        }
                    }
                    else
                    {
                        map.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                }
            }
            map.Apply();
            mapImage.sprite = Sprite.Create(map, new Rect(0, 0, mapWidthPixels, mapHeightPixels), new Vector2(0.5f, 0.5f));
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

            coords.fontSize = 30;
            coords.color = Color.white;
            coords.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            coords.horizontalOverflow = HorizontalWrapMode.Overflow;
            coords.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = textGo.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(0, Screen.height / 2 - 150, 0);

            var imageGo = new GameObject("MapImage");
            imageGo.transform.SetParent (cv.transform, false);

            mapImage = imageGo.AddComponent<Image>();
            mapImage.color = Color.yellow;

            rt = imageGo.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(0, 0, 0);
            rt.sizeDelta = new Vector2(renderWidth, renderHeight);
        }
    }
}
