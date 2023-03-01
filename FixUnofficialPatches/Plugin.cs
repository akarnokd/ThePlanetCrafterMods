using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;
using System.IO;
using System;

namespace FixUnofficialPatches
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunofficialpatches", "(Fix) Unofficial Patches", "1.0.0.5")]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            SaveFilesSelectorScrollFix();
        }

        static SaveFilesSelector saveFilesSelectorInstance;
        static FieldInfo saveFilesSelectorObjectsInList;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.Start))]
        static void SaveFilesSelector_Start(SaveFilesSelector __instance)
        {
            saveFilesSelectorInstance = __instance;
            saveFilesSelectorObjectsInList = AccessTools.Field(typeof(SaveFilesSelector), "objectsInList");
        }

        static void SaveFilesSelectorScrollFix()
        {
            if (saveFilesSelectorInstance != null && saveFilesSelectorInstance.filesListContainer.transform.parent.gameObject.activeSelf)
            {
                var scrollBox = saveFilesSelectorInstance.GetComponentInChildren<ScrollRect>();
                if (scrollBox != null)
                {
                    var scroll = Mouse.current.scroll.ReadValue();
                    if (scroll.y != 0)
                    {
                        var counts = (List<GameObject>)saveFilesSelectorObjectsInList.GetValue(saveFilesSelectorInstance);
                        if (counts != null && counts.Count != 0)
                        {
                            if (scroll.y < 0)
                            {
                                scrollBox.verticalNormalizedPosition -= 1f / counts.Count;
                            }
                            else if (scroll.y > 0)
                            {
                                scrollBox.verticalNormalizedPosition += 1f / counts.Count;
                            }
                        }
                    }
                }
            }
        }

        /*
         * Fixed in 0.5.005
        /// <summary>
        /// Fixes the lack of localization Id when viewing a Craft Station T2, so the window title is not properly updated.
        /// </summary>
        /// <param name="__instance">The ActionCrafter instance of the station object</param>
        /// <param name="___titleLocalizationId">The field hosting the localization id</param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionCrafter), nameof(ActionCrafter.OnAction))]
        static void ActionCrafter_OnAction(ActionCrafter __instance, ref string ___titleLocalizationId)
        {
            if (__instance.GetCrafterIdentifier() == DataConfig.CraftableIn.CraftStationT2)
            {
                ___titleLocalizationId = "GROUP_NAME_CraftStation1";
            }
        }
        */

        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerAimController), "HandleAiming")]
        static void PlayerAimController_HandleAiming(Ray ___aimingRay, float ___distanceHitLimit, int ___layerMask)
        {
            if (Physics.Raycast(___aimingRay, out var raycastHit, ___distanceHitLimit, ___layerMask))
            {
                logger.LogInfo("Looking at " + raycastHit.transform.gameObject.name + " (" + ___layerMask + ")");
            }
            else
            {
                logger.LogInfo("No hits");
            }
        }
        */

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WindowsHandler), "OpenDevBranch")]
        static bool WindowsHandler_OpenDevBranch()
        {
            if (GameConfig.IsDevBranch)
            {
                string once = Application.persistentDataPath + "/devbranch-warning-once.txt";
                if (File.Exists(once))
                {
                    return false;
                }
                File.WriteAllText(once, DateTime.UtcNow.ToString("o"));
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineFloater), "SetReferencePositionOnSurface")]
        static bool MachineFloater_SetReferencePositionOnSurface(MachineFloater __instance,
            ref Vector3 ___referencePosition,
            WorldObjectAssociated ___worldObjectAsso)
        {
            Vector3 vector = new Vector3(__instance.transform.position.x, __instance.transform.position.y + 5f, __instance.transform.position.z);
            Vector3 vector2 = Vector3.up * -1f;
            RaycastHit raycastHit;
            if (Physics.Raycast(new Ray(vector, vector2), out raycastHit, 105f, LayerMask.GetMask(new string[] { GameConfig.layerWaterName })))
            {
                Vector3 vector3 = Vector3.up * -0.25f;
                ___referencePosition = raycastHit.point + vector3;
                if (___worldObjectAsso != null)
                {
                    ___worldObjectAsso.GetWorldObject()?.SetPositionAndRotation(___referencePosition, Quaternion.identity);
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FlockChildInsect), nameof(FlockChildInsect.UpdatePosition))]
        static bool FlockChildInsect_UpdatePosition(FlockController ___flockController)
        {
            return ___flockController != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrower), "OnVegetableGrabed")]
        static bool MachineGrower_OnVegetableGrabed()
        {
            return Managers.GetManager<PlayersManager>()?.GetActivePlayerController() != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineConvertRecipe), nameof(MachineConvertRecipe.CheckIfFullyGrown))]
        static bool MachineConvertRecipe_CheckIfFullyGrown(
            WorldObject ___worldObject
        )
        {
            return ___worldObject != null;
        }

        // Bug in 0.7.001 when loading a completely new world
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        static void PlanetLoader_HandleDataAfterLoad(ref PlanetIsLoaded ___planetIsLoaded)
        {
            if (___planetIsLoaded == null)
            {
                ___planetIsLoaded = () => { };
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnvironmentVolume), "Start")]
        static void EnvironmentVolue_Start(EnvironmentVolume __instance)
        {
            if (__instance.environmentVolumeVariables == null)
            {
                logger.LogError(__instance.gameObject.name + " id " + __instance.GetInstanceID() + ", environmentVolumeVariables == null");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnvironmentVolume), "CalculateLerpRelativeToPositionInCollider")]
        static bool EnvironmentVolue_CalculateLerpRelativeToPositionInCollider(EnvironmentVolume __instance)
        {
            if (__instance.liveEnvironmentVolumeVariables == null)
            {
                // logger.LogError(__instance.name + ", liveEnvironmentVolumeVariables == null");
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnvironmentVolume), "OnTriggerExit")]
        static bool EnvironmentVolue_OnTriggerExit(EnvironmentVolume __instance)
        {
            if (__instance.liveEnvironmentVolumeVariables == null)
            {
                // logger.LogError(__instance.name + ", liveEnvironmentVolumeVariables == null");
                return false;
            }
            return true;
        }
    }
}
