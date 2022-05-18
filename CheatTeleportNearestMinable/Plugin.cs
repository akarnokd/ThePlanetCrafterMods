using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace CheatTeleportNearestMinable
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatteleportnearestminable", "(Cheat) Teleport To Nearest Minable", "1.0.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// List of comma-separated resource ids to look for.
        /// </summary>
        private static HashSet<string> resourceSet;

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
            Logger.LogInfo($"Plugin is loaded!");

            string resourceSetStr = Config.Bind("General", "ResourceSet", defaultResourceSet, "List of comma-separated resource ids to look for.").Value;

            resourceSet = new HashSet<string>(resourceSetStr.Split(new char[] { ',' }));

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnShowFeedbackDispatcher))]
        static bool PlayerInputDispatcher_OnShowFeedbackDispatcher()
        {
            PlayerMainController activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            Vector3 playerPos = activePlayerController.transform.position;
            ActionMinable[] array = UnityEngine.Object.FindObjectsOfType<ActionMinable>();
            
            Vector3 nearest = Vector3.zero;
            float minDistance = 0f;
            bool foundPlaced = false;
            string gid = "";
            GameObject foundgo = null;
            WorldObject foundwo = null;
            foreach (ActionMinable am in array)
            {
                WorldObjectAssociated woa = am.GetComponent<WorldObjectAssociated>();
                if (woa != null)
                {
                    WorldObject wo = woa.GetWorldObject();
                    if (wo != null && resourceSet.Contains(wo.GetGroup().GetId()))
                    {
                        Vector3 p = am.gameObject.transform.position;
                        if (p.x != 0f && p.y != 0f && p.z != 0f)
                        {
                            if (!foundPlaced)
                            {
                                foundPlaced = true;
                                nearest = p;
                                minDistance = Vector3.Distance(playerPos, p);
                                gid = wo.GetGroup().GetId();
                                foundgo = am.gameObject;
                                foundwo = wo;
                            }
                            else
                            {
                                float d = Vector3.Distance(playerPos, p);
                                if (d < minDistance)
                                {
                                    nearest = p;
                                    minDistance = d;
                                    gid = wo.GetGroup().GetId();
                                    foundgo = am.gameObject;
                                    foundwo = wo;
                                }
                            }
                        }
                    }
                }
            }

            if (foundPlaced)
            {
                bool isShift = Keyboard.current[Key.LeftShift].isPressed;
                bool isCtrl = Keyboard.current[Key.LeftCtrl].isPressed;
                if (isShift || isCtrl)
                {
                    if (!activePlayerController.GetPlayerBackpack().GetInventory().AddItem(foundwo))
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_InventoryFull", 1f, "");
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(foundgo);
                        foundwo.SetDontSaveMe(false);
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, string.Concat(new object[]
                        {
                            "< ",
                            (int)nearest.x,
                            ", ",
                            (int)nearest.y,
                            ", ",
                            (int)nearest.z,
                            "  >  ",
                            gid,
                            " --- PICKED UP ---"
                        }));
                    }
                }
                else
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, string.Concat(new object[]
                    {
                        "< ",
                        (int)nearest.x,
                        ", ",
                        (int)nearest.y,
                        ", ",
                        (int)nearest.z,
                        "  >  ",
                        gid
                    }));
                }
                if (!isCtrl)
                {
                    activePlayerController.SetPlayerPlacement(nearest, activePlayerController.transform.rotation);
                }
                return false;
            }
            Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "No resources found in this area");

            return false;
        }


    }
}
