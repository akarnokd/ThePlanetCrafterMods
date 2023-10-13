using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine.UI;

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
        private static ConfigEntry<string> cycleResourceKey;
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

        static readonly string defaultResourceSet = string.Join(",", new string[]
        {
            "Cobalt",
            "Silicon",
            "Iron",
            "ice", // it is not capitalized in the game
            "Magnesium",
            "Titanium",
            "Aluminium",
            "Uranim", // it is misspelled in the game
            "Iridium",
            "Alloy",
            "Zeolite",
            "Osmium",
            "Sulfur",
            "PulsarQuartz",
            "PulsarShard"
        });

        static readonly string defaultLarvaeSet = string.Join(",", new string[]
        {
            "LarvaeBase1",
            "LarvaeBase2",
            "LarvaeBase3",
            "Butterfly11Larvae",
            "Butterfly12Larvae",
            "Butterfly13Larvae",
            "Butterfly14Larvae",
            "Butterfly15Larvae",
            "Butterfly16Larvae",
            "Butterfly17Larvae",
            "Butterfly18Larvae",
            "Butterfly19Larvae"
        });

        static readonly string defaultFishSet = string.Join(",", new string[]
        {
            "Fish1Eggs",
            "Fish2Eggs",
            "Fish3Eggs",
            "Fish4Eggs",
            "Fish5Eggs",
            "Fish6Eggs",
            "Fish7Eggs",
            "Fish8Eggs",
            "Fish9Eggs",
            "Fish10Eggs",
            "Fish11Eggs",
            "Fish12Eggs"
        });

        static readonly string defaultFrogSet = string.Join(",", new string[]
        {
            "Frog1Eggs",
            "Frog2Eggs",
            "Frog3Eggs",
            "Frog4Eggs",
            "Frog5Eggs",
            "Frog6Eggs",
            "Frog7Eggs",
            "Frog8Eggs",
            "Frog9Eggs",
            "Frog10Eggs",
            "FrogGoldEggs",
        });

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

        static List<GameObjectTTL> scannerImageList = new List<GameObjectTTL>();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            radius = Config.Bind("General", "Radius", 30, "Specifies how far to look for resources.");
            stretchY = Config.Bind("General", "StretchY", 1, "Specifies how high the resource image to stretch.");
            resourceSetStr = Config.Bind("General", "ResourceSet", defaultResourceSet, "List of comma-separated resource ids to look for.");
            cycleResourceKey = Config.Bind("General", "CycleResourceKey", "X", "Key used for cycling resources from the set");
            larvaeSetStr = Config.Bind("General", "LarvaeSet", defaultLarvaeSet, "List of comma-separated larvae ids to look for.");
            fishSetStr = Config.Bind("General", "FishSet", defaultFishSet, "List of comma-separated fish ids to look for.");
            frogSetStr = Config.Bind("General", "FrogSet", defaultFrogSet, "List of comma-separated frog ids to look for.");
            lineIndicatorLength = Config.Bind("General", "LineIndicatorLength", 5f, "If nonzero, a thin white bar will appear and point to the resource");
            timeToLive = Config.Bind("General", "TimeToLive", 15f, "How long the resource indicators should remain visible, in seconds.");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p != null)
            {
                PlayerMainController pm = p.GetActivePlayerController();
                var wh = Managers.GetManager<WindowsHandler>();
                if (pm != null && !wh.GetHasUiOpen())
                {
                    PropertyInfo pi = typeof(Key).GetProperty(cycleResourceKey.Value.ToString().ToUpper());
                    Key k = Key.X;
                    if (pi != null)
                    {
                        k = (Key)pi.GetRawConstantValue();
                    }

                    //
                    if (Keyboard.current[k].wasPressedThisFrame)
                    {
                        List<string> scanSetList = new();
                        scanSetList.AddRange(resourceSetStr.Value.Split(','));
                        scanSetList.AddRange(larvaeSetStr.Value.Split(','));
                        scanSetList.AddRange(fishSetStr.Value.Split(','));
                        scanSetList.AddRange(frogSetStr.Value.Split(','));

                        if (Keyboard.current[Key.LeftShift].isPressed)
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
                                currentResource = scanSetList[scanSetList.Count - 1];
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
            HashSet<string> scanSet = new HashSet<string>();
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
            List<Component> candidates = new();
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
                        GameObject iconGo = new GameObject("ScannerImage-" + gid);
                        SpriteRenderer spriteRenderer = iconGo.AddComponent<SpriteRenderer>();
                        spriteRenderer.sprite = icon;
                        spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                        iconGo.transform.position = new Vector3(resourcePosition.x, resourcePosition.y + 3f, resourcePosition.z);
                        iconGo.transform.rotation = resource.gameObject.transform.rotation;
                        iconGo.transform.localScale = new Vector3(1f, sy, 1f);
                        iconGo.transform.LookAt(player, player.up);
                        iconGo.SetActive(true);

                        GameObjectTTL go = new GameObjectTTL();
                        go.resource = resource.gameObject;
                        go.icon = iconGo;
                        go.time = Time.time + timeToLive.Value;

                        float barLen = lineIndicatorLength.Value;
                        if (barLen > 0)
                        {
                            float barWidth = 0.1f;
                            GameObject bar1 = new GameObject("ScannerImage-" + gid + "-Bar1");
                            var image1 = bar1.AddComponent<SpriteRenderer>();
                            image1.sprite = icon;
                            image1.color = new Color(1f, 1f, 1f, 1f);
                            bar1.transform.position = new Vector3(resourcePosition.x, resourcePosition.y + barLen / 2, resourcePosition.z);
                            bar1.transform.rotation = Quaternion.identity;
                            bar1.transform.localScale = new Vector3(barWidth, barLen, barWidth);
                            bar1.SetActive(true);

                            GameObject bar2 = new GameObject("ScannerImage-" + gid + "-Bar2");
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
    }
}
