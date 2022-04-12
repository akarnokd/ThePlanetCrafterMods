using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace CheatNearbyResourcesHighlight
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatnearbyresourceshighlight", "(Cheat) Highlight Nearby Resources", "1.0.0.1")]
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
            "Sulfur"
        });

        class GameObjectTTL
        {
            public GameObject gameObject;
            public float time;
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

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p != null)
            {
                PlayerMainController pm = p.GetActivePlayerController();
                if (pm != null)
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
                        string[] resourceSetArray = resourceSetStr.Value.Split(',');
                        if (Keyboard.current[Key.LeftShift].isPressed)
                        {
                            if (currentResource == null)
                            {
                                currentResource = resourceSetArray[0];
                            }
                            else
                            {
                                int idx = Array.IndexOf(resourceSetArray, currentResource);
                                if (idx < resourceSetArray.Length - 1)
                                {
                                    currentResource = resourceSetArray[idx + 1];
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
                                currentResource = resourceSetArray[resourceSetArray.Length - 1];
                            }
                            else
                            {
                                int idx = Array.IndexOf(resourceSetArray, currentResource);
                                if (idx > 0)
                                {
                                    currentResource = resourceSetArray[idx - 1];
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
                        if (Time.time >= gameObjectTTL.time)
                        {
                            gameObjectTTL.gameObject.SetActive(false);
                            UnityEngine.Object.Destroy(gameObjectTTL.gameObject);
                            scannerImageList.RemoveAt(i);
                        }
                        else
                        {
                            gameObjectTTL.gameObject.transform.LookAt(player, player.up);
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
            HashSet<string> resourceSet = new HashSet<string>();
            if (currentResource != null)
            {
                resourceSet.Add(currentResource);
            }
            else
            {
                foreach (string r in resourceSetStr.Value.Split(','))
                {
                    resourceSet.Add(r);
                }
            }

            // hide any previously shown scanner images.
            foreach (GameObjectTTL go in scannerImageList)
            {
                go.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(go.gameObject);
            }
            scannerImageList.Clear();

            // where is the player?
            Transform player = Managers.GetManager<PlayersManager>().GetActivePlayerController().transform;
            Vector3 playerPosition = player.position;
            int c = 0;


            // Where are the minable objects?
            ActionMinable[] minableResources = UnityEngine.Object.FindObjectsOfType<ActionMinable>();
            foreach (ActionMinable resource in minableResources)
            {
                WorldObjectAssociated component = resource.GetComponent<WorldObjectAssociated>();
                if (component != null)
                {
                    WorldObject worldObject = component.GetWorldObject();
                    if (worldObject != null && resourceSet.Contains(worldObject.GetGroup().GetId()))
                    {
                        Vector3 resourcePosition = resource.gameObject.transform.position;
                        if (resourcePosition.x != 0f && resourcePosition.y != 0f && resourcePosition.z != 0f
                            && (resourcePosition - playerPosition).sqrMagnitude < maxRangeSqr)
                        {
                            GameObject gameObject = new GameObject("ScannerImage");
                            SpriteRenderer spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                            spriteRenderer.sprite = worldObject.GetGroup().GetImage();
                            spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                            gameObject.transform.position = new Vector3(resourcePosition.x, resourcePosition.y + 3f, resourcePosition.z);
                            gameObject.transform.rotation = resource.gameObject.transform.rotation;
                            gameObject.transform.localScale = new Vector3(1f, sy, 1f);
                            gameObject.transform.LookAt(player, player.up);
                            gameObject.SetActive(true);

                            GameObjectTTL go = new GameObjectTTL();
                            go.gameObject = gameObject;
                            go.time = Time.time + 15f;
                            scannerImageList.Add(go);
                            c++;
                        }
                    }
                }
            }
            Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 2f, "Found resources: " + c + " x " + (currentResource ?? "any"));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnToggleLightDispatcher))]
        static bool PlayerInputDispatcher_OnToggleLightDispatcher()
        {
            if (Keyboard.current[Key.LeftCtrl].isPressed)
            {
                ScanForResourcesNow();
                return false;
            }
            return true;
        }
    }
}
