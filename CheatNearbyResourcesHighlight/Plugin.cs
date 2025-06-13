// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using LibCommon;
using System;

namespace CheatNearbyResourcesHighlight
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatnearbyresourceshighlight", "(Cheat) Highlight Nearby Resources", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// Specifies how far to look for resources.
        /// </summary>
        private static ConfigEntry<int> radius;
        /// <summary>
        /// Specifies how high the resource image to stretch.
        /// </summary>
        private static ConfigEntry<int> stretchY;
        /// <summary>
        /// Key used for cycling resources.
        /// </summary>
        private static ConfigEntry<string> scanKey;
        /// <summary>
        /// List of comma-separated resource ids to look for.
        /// </summary>
        private static ConfigEntry<string> resourceSetStr;

        private static ConfigEntry<string> larvaeSetStr;

        private static ConfigEntry<string> fishSetStr;

        private static ConfigEntry<string> frogSetStr;

        private static ConfigEntry<float> lineIndicatorLength;

        private static ConfigEntry<float> timeToLive;

        /// <summary>
        /// Which specific resource to hightlight only
        /// </summary>
        private static string currentResource;

        class GameObjectTTL
        {
            public GameObject resource;
            public GameObject icon;
            public GameObject bar1;
            public GameObject bar2;
            public float time;

            public void Destroy()
            {
                resource = null;
                UnityEngine.Object.Destroy(icon);
                UnityEngine.Object.Destroy(bar1);
                UnityEngine.Object.Destroy(bar2);
            }

            public void LookAt(Transform target, Vector3 up)
            {
                icon.transform.LookAt(target, up);
            }
        }

        static readonly List<GameObjectTTL> scannerImageList = [];

        static InputAction scanInput;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            radius = Config.Bind("General", "Radius", 30, "Specifies how far to look for resources.");
            stretchY = Config.Bind("General", "StretchY", 1, "Specifies how high the resource image to stretch.");
            resourceSetStr = Config.Bind("General", "ResourceSet", StandardResourceSets.defaultOres, "List of comma-separated resource ids to look for.");
            scanKey = Config.Bind("General", "CycleResourceKey", "<Keyboard>/X", "Key used for cycling resources from the set");
            larvaeSetStr = Config.Bind("General", "LarvaeSet", StandardResourceSets.defaultLarvae, "List of comma-separated larvae ids to look for.");
            fishSetStr = Config.Bind("General", "FishSet", StandardResourceSets.defaultFish, "List of comma-separated fish ids to look for.");
            frogSetStr = Config.Bind("General", "FrogSet", StandardResourceSets.defaultFrogs, "List of comma-separated frog ids to look for.");
            lineIndicatorLength = Config.Bind("General", "LineIndicatorLength", 5f, "If nonzero, a thin white bar will appear and point to the resource");
            timeToLive = Config.Bind("General", "TimeToLive", 15f, "How long the resource indicators should remain visible, in seconds.");

            UpdateKeyBinding();

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        public void Update()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p != null)
            {
                PlayerMainController pm = p.GetActivePlayerController();
                var wh = Managers.GetManager<WindowsHandler>();
                if (pm != null && !wh.GetHasUiOpen())
                {
                    //
                    if (scanInput.WasPressedThisFrame())
                    {
                        List<string> scanSetList =
                        [
                            .. resourceSetStr.Value.Split(','),
                            .. larvaeSetStr.Value.Split(','),
                            .. fishSetStr.Value.Split(','),
                            .. frogSetStr.Value.Split(','),
                        ];

                        if (Keyboard.current[Key.LeftCtrl].isPressed && Keyboard.current[Key.LeftShift].isPressed)
                        {
                            currentResource = null;
                        }
                        else if (Keyboard.current[Key.LeftShift].isPressed)
                        {
                            if (currentResource == null)
                            {
                                currentResource = scanSetList[0];
                            }
                            else
                            {
                                int idx = scanSetList.IndexOf(currentResource);
                                if (idx < scanSetList.Count - 1)
                                {
                                    currentResource = scanSetList[idx + 1];
                                }
                                else
                                {
                                    currentResource = null;
                                }
                            }
                        }
                        else if (Keyboard.current[Key.LeftCtrl].isPressed)
                        {
                            if (currentResource == null)
                            {
                                currentResource = scanSetList[^1];
                            }
                            else
                            {
                                int idx = scanSetList.IndexOf(currentResource);
                                if (idx > 0)
                                {
                                    currentResource = scanSetList[idx - 1];
                                }
                                else
                                {
                                    currentResource = null;
                                }
                            }
                        }
                        
                            
                        ScanForResourcesNow();
                    }

                    Transform player = pm.transform;
                    for (int i = scannerImageList.Count - 1; i >= 0; i--)
                    {
                        GameObjectTTL gameObjectTTL = scannerImageList[i];
                        if (Time.time >= gameObjectTTL.time || (gameObjectTTL.resource == null || !gameObjectTTL.resource.activeSelf))
                        {
                            gameObjectTTL.Destroy();
                            scannerImageList.RemoveAt(i);
                        }
                        else
                        {
                            gameObjectTTL.LookAt(player, player.up);
                        }
                    }
                }
            }
        }

        static void ScanForResourcesNow()
        {
            // Prepare configuration values
            float maxRangeSqr = radius.Value * radius.Value;
            float sy = stretchY.Value;
            // prepare resource sets to highlight
            var scanSet = new HashSet<string>();
            bool hasResources = false;
            bool hasLarvae = false;
            bool hasFish = false;
            bool hasFrog = false;
            if (currentResource != null)
            {
                scanSet.Add(currentResource);
                hasResources = true;
                hasLarvae = true;
            }
            else
            {
                foreach (string r in resourceSetStr.Value.Split(','))
                {
                    scanSet.Add(r);
                    hasResources = true;
                }
                foreach (string lr in larvaeSetStr.Value.Split(','))
                {
                    scanSet.Add(lr);
                    hasLarvae = true;
                }
                foreach (string lr in fishSetStr.Value.Split(','))
                {
                    scanSet.Add(lr);
                    hasFish = true;
                }
                foreach (string lr in frogSetStr.Value.Split(','))
                {
                    scanSet.Add(lr);
                    hasFrog = true;
                }
            }

            // hide any previously shown scanner images.
            foreach (GameObjectTTL go in scannerImageList)
            {
                go.Destroy();
            }
            scannerImageList.Clear();

            var playerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            // where is the player?
            Transform player = playerController.transform;
            Vector3 playerPosition = player.position;
            int c = 0;


            // Where are the minable objects?
            List<Component> candidates = [];
            if (hasResources)
            {
                candidates.AddRange(FindObjectsByType<ActionMinable>(FindObjectsSortMode.None));
            }
            if (hasLarvae || hasFish || hasFrog)
            {
                candidates.AddRange(FindObjectsByType<ActionGrabable>(FindObjectsSortMode.None));
            }

            foreach (Component resource in candidates)
            {
                var gid = "";
                Sprite icon = null;

                if (resource.TryGetComponent<WorldObjectFromScene>(out var woScene)) {
                    var gd = woScene.GetGroupData();
                    gid = gd.id;
                    icon = gd.icon;
                }
                else
                if (resource is ActionGrabable && resource.TryGetComponent<WorldObjectAssociated>(out var woa))
                {
                    var wo = woa.GetWorldObject();
                    if (wo != null)
                    {
                        var gd = wo.GetGroup();
                        gid = gd.id;
                        icon = gd.GetImage();
                    }
                }
                if (gid != "" && scanSet.Contains(gid))
                {
                    Vector3 resourcePosition = resource.gameObject.transform.position;
                    if (resourcePosition.x != 0f && resourcePosition.y != 0f && resourcePosition.z != 0f
                        && (resourcePosition - playerPosition).sqrMagnitude < maxRangeSqr)
                    {
                        var iconGo = new GameObject("ScannerImage-" + gid);
                        var spriteRenderer = iconGo.AddComponent<SpriteRenderer>();
                        spriteRenderer.sprite = icon;
                        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                        iconGo.transform.position = new Vector3(resourcePosition.x, resourcePosition.y + 3f, resourcePosition.z);
                        iconGo.transform.rotation = resource.gameObject.transform.rotation;
                        iconGo.transform.localScale = new Vector3(1f, sy, 1f);
                        iconGo.transform.LookAt(player, player.up);
                        iconGo.SetActive(true);

                        var go = new GameObjectTTL
                        {
                            resource = resource.gameObject,
                            icon = iconGo,
                            time = Time.time + timeToLive.Value
                        };

                        float barLen = lineIndicatorLength.Value;
                        if (barLen > 0)
                        {
                            float barWidth = 0.1f;
                            var bar1 = new GameObject("ScannerImage-" + gid + "-Bar1");
                            var image1 = bar1.AddComponent<SpriteRenderer>();
                            image1.sprite = icon;
                            image1.color = new Color(1f, 1f, 1f, 1f);
                            bar1.transform.position = new Vector3(resourcePosition.x, resourcePosition.y + barLen / 2, resourcePosition.z);
                            bar1.transform.rotation = Quaternion.identity;
                            bar1.transform.localScale = new Vector3(barWidth, barLen, barWidth);
                            bar1.SetActive(true);

                            var bar2 = new GameObject("ScannerImage-" + gid + "-Bar2");
                            var image2 = bar2.AddComponent<SpriteRenderer>();
                            image2.sprite = icon;
                            image2.color = new Color(1f, 1f, 1f, 1f);
                            bar2.transform.position = new Vector3(resourcePosition.x, resourcePosition.y + barLen / 2, resourcePosition.z);
                            bar2.transform.localScale = new Vector3(barWidth, barLen, barWidth);
                            bar2.transform.rotation = Quaternion.Euler(0, 90, 0);
                            bar2.SetActive(true);

                            go.bar1 = bar1;
                            go.bar2 = bar2;
                        }

                        scannerImageList.Add(go);
                        c++;
                    }
                }
            }
            Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Found resources: " + c + " x " + (currentResource ?? "any"));
        }

        static void UpdateKeyBinding()
        {
            if (!scanKey.Value.StartsWith("<", StringComparison.Ordinal))
            {
                scanKey.Value = "<Keyboard>/" + scanKey.Value;
            }
            scanInput = new InputAction("Scan and highlight resources", binding: scanKey.Value);
            scanInput.Enable();
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            UpdateKeyBinding();
        }

    }
}
