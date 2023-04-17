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
using System.Linq;

namespace FixUnofficialPatches
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunofficialpatches", "(Fix) Unofficial Patches", PluginInfo.PLUGIN_VERSION)]
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(EnvironmentVolume), "CalculateLerpRelativeToPositionInCollider")]
        static bool EnvironmentVolue_CalculateLerpRelativeToPositionInCollider(EnvironmentVolume __instance)
        {
            if (__instance.liveEnvironmentVolumeVariables == null)
            {
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
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Localization), nameof(Localization.GetLocalizedString))]
        static bool Localization_GetLocalizedString(string stringCode, ref string __result)
        {
            if (stringCode == null)
            {
                __result = "";
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TerrainVisualsHandler), "GetTerrainLayerDataByName")]
        static bool TerrainVisualsHandler_GetTerrainLayerDataByName(
            Dictionary<string, TerrainLayerData> ___terrainLayerDatas,
            ref TerrainLayerData __result)
        {
            if (___terrainLayerDatas == null)
            {
                __result = null;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticEntity), nameof(LogisticEntity.ClearSupplyGroups))]
        static bool LogisticEntity_ClearSupplyGroups(LogisticEntity __instance)
        {
            return __instance.HasSupplyGroups();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticEntity), nameof(LogisticEntity.ClearDemandGroups))]
        static bool LogisticEntity_ClearDemandGroups(LogisticEntity __instance)
        {
            return __instance.HasDemandGroups();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), nameof(Drone.UpdateState))]
        static void Drone_UpdateState(LogisticTask ___logisticTask)
        {
            if (___logisticTask != null)
            {
                var state = ___logisticTask.GetTaskState();
                if (state == LogisticData.TaskState.ToDemand)
                {
                    var go = ___logisticTask.GetDemandInventoryWorldObject()?.GetGameObject();
                    if (go == null)
                    {
                        ___logisticTask.SetTaskState(LogisticData.TaskState.Done);
                    }
                }
                if (state == LogisticData.TaskState.ToSupply)
                {
                    var go = ___logisticTask.GetSupplyInventoryWorldObject()?.GetGameObject();
                    if (go == null)
                    {
                        ___logisticTask.SetTaskState(LogisticData.TaskState.Done);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerLarvaeAround), "CleanFarAwayLarvae")]
        static void PlayerLarvaeAround_CleanFarAwayLarvae(List<GameObject> ___larvaesSpawned)
        {
            for (int i = ___larvaesSpawned.Count - 1; i >= 0; i--)
            {
                if (___larvaesSpawned[i] == null)
                {
                    ___larvaesSpawned.RemoveAt(i);
                }
            }
        }

    }
}
