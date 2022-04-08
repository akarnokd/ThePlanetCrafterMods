using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace CheatNearbyResourcesHighlight
{
    [BepInPlugin("com.github.akarnokd.theplanetcraftermods.cheatnearbyresourceshighlight", "Highlight Nearby Resources (Cheat)", "1.0.0.0")]
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
        /// List of comma-separated resource ids to look for.
        /// </summary>
        private static ConfigEntry<string> resourceSet;

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

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin com.github.akarnokd.theplanetcraftermods.cheatnearbyresourceshighlight is loaded!");

            radius = Config.Bind("General", "Radius", 30, "Specifies how far to look for resources.");
            stretchY = Config.Bind("General", "StretchY", 1, "Specifies how high the resource image to stretch.");
            resourceSet = Config.Bind("General", "ResourceSet", defaultResourceSet, "List of comma-separated resource ids to look for.");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnToggleLightDispatcher))]
        static bool PlayerInputDispatcher_OnToggleLightDispatcher()
        {
            if (Keyboard.current[Key.LeftCtrl].isPressed)
            {
                // Prepare configuration values
                float maxRangeSqr = radius.Value * radius.Value;
                HashSet<string> hashSet = new HashSet<string>(resourceSet.Value.Split(new char[] { ',' }));
                float sy = stretchY.Value;
        
                // hide any previously shown scanner images.
                foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
                {
                    if (go.name == "ScannerImage")
                    {
                        go.SetActive(false);
                    }
                }

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
                        if (worldObject != null && hashSet.Contains(worldObject.GetGroup().GetId()))
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
                                UnityEngine.Object.Destroy(gameObject, 15f);
                                c++;
                            }
                        }
                    }
                }
                Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 1f, "Found resources: " + c);
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), "Update")]
        static void PlayerInputDispatcher_Update()
        {
            Transform player = Managers.GetManager<PlayersManager>().GetActivePlayerController().transform;
            foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (go.name == "ScannerImage")
                {
                    go.transform.LookAt(player, player.up);
                }
            }
        }
    }
}
