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
using TMPro;
using System.Globalization;
using System.Collections;
using BepInEx.Configuration;

namespace FixUnofficialPatches
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunofficialpatches", "(Fix) Unofficial Patches", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static MethodInfo PlayerDirectVolumeOnTriggerExit;

        static ConfigEntry<bool> enableTeleportFix;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            enableTeleportFix = Config.Bind("General", "TeleportFix", true, "Enable the workaround for teleporting around and breaking larvae spawns");

            PlayerDirectVolumeOnTriggerExit = AccessTools.Method(typeof(PlayerDirectEnvironment), "OnTriggerExit", new Type[] { typeof(Collider) });

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

        /*
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
        */

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ScreenTerraStage), "RefreshDisplay", new Type[0])]
        static void ScreenTerraStage_RefreshDisplay(
            ScreenTerraStage __instance, TerraformStagesHandler ___terraformStagesHandler, ref TerraformStage ___previousCurrentStage)
        {
            TerraformStage currentGlobalStage = ___terraformStagesHandler.GetCurrentGlobalStage();
            TerraformStage nextGlobalStage = ___terraformStagesHandler.GetNextGlobalStage();

            if (currentGlobalStage != null && nextGlobalStage != null)
            {
                if (currentGlobalStage != nextGlobalStage)
                {
                    __instance.percentageProcess.text = ___terraformStagesHandler.GetNextGlobalStageCompletion().ToString("F2") + "%";
                }
                else
                {
                    __instance.percentageProcess.text = "N/A";
                }
            }
            else
            {
                __instance.percentageProcess.text = "N/A";
            }

            if (currentGlobalStage != null)
            {
                __instance.currentStageImage.sprite = currentGlobalStage.GetStageImage();
                __instance.currentStageName.text = Readable.GetTerraformStageName(currentGlobalStage);
            }
            else
            {
                __instance.currentStageImage.sprite = null;
                __instance.currentStageName.text = "N/A";
            }
            if (nextGlobalStage != null && nextGlobalStage != currentGlobalStage)
            {
                __instance.nextStageImage.sprite = nextGlobalStage.GetStageImage();
                __instance.nextStageName.text = Readable.GetTerraformStageName(nextGlobalStage);
            }
            else
            {
                __instance.nextStageImage.sprite = null;
                __instance.nextStageName.text = "N/A";
            }
        }

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticStationDistanceToTask), nameof(LogisticStationDistanceToTask.CompareTo))]
        static bool LogisticStationDistanceToTask_CompareTo(object obj, float ___distanceToSupply, ref int __result)
        {
            var dist = (obj as LogisticStationDistanceToTask).GetDistance();
            if (dist < ___distanceToSupply)
            {
                __result = 1;
            }
            else if (dist > ___distanceToSupply)
            {
                __result = -1;
            }
            else
            {
                __result = 0;
            }
            return false;
        }
        */

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDroneStation), "OnDestroy")]
        static bool MachineDroneStation_OnDestroy()
        {
            return Managers.GetManager<LogisticManager>() != null;
        }

        static readonly Color colorTransparent = new(0, 0, 0, 0);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DataTreatments), nameof(DataTreatments.ColorToString))]
        static bool DataTreatments_ColorToString(ref string __result, in Color _color, char ___colorDelimiter)
        {
            if (_color == colorTransparent)
            {
                __result = "";
            }
            else
            {
                __result = _color.r.ToString(CultureInfo.InvariantCulture)
                        + ___colorDelimiter
                        + _color.g.ToString(CultureInfo.InvariantCulture)
                        + ___colorDelimiter
                        + _color.b.ToString(CultureInfo.InvariantCulture)
                        + ___colorDelimiter
                        + _color.a.ToString(CultureInfo.InvariantCulture)
                    ;
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DataTreatments), nameof(DataTreatments.ParseStringColor))]
        static void DataTreatments_ParseStringColor(ref string _float)
        {
            _float = _float.Replace('/', '.');
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrower), "InstantiatedGameObjectFromInventory")]
        static bool InstantiatedGameObjectFromInventory(GameObject ___spawnPoint)
        {
            return ___spawnPoint != null;
        }

        static List<Collider> exitedColliders;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerMainController), "SetPlayerPlacement")]
        static bool PlayerMainController_SetPlayerPlacement(PlayerMainController __instance,
            Vector3 _position, Quaternion _rotation)
        {
            if (enableTeleportFix.Value)
            {
                var beforeColliders = Physics.OverlapSphere(__instance.transform.position, 0.1f);
                exitedColliders = new();

                __instance.transform.position = _position;
                __instance.transform.rotation = _rotation;
                Physics.SyncTransforms();
                PlayerFallDamage component = __instance.GetComponent<PlayerFallDamage>();
                if (component != null)
                {
                    component.ResetLastGroundPlace();
                }

                Managers.GetManager<DisablerManager>().ForceCheckAllDisablers();

                __instance.StartCoroutine(SetPlayerPlacementAfter(1, beforeColliders, __instance));

                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerDirectEnvironment), "OnTriggerExit")]
        static void PlayerDirectEnvironment_OnTriggerExit(Collider other)
        {
            exitedColliders?.Add(other);
        }

        static IEnumerator SetPlayerPlacementAfter(
            int frames,
            Collider[] beforeColliders,
            PlayerMainController __instance)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }

            var pde = __instance?.GetComponent<PlayerDirectEnvironment>();
            if (pde != null)
            {
                var afterColliders = Physics.OverlapSphere(__instance.transform.position, 0.1f);

                foreach (var bc in beforeColliders)
                {
                    bool inAfterColliders = Array.IndexOf(afterColliders, bc) != -1;
                    bool hasExitedNormally = exitedColliders == null || exitedColliders.IndexOf(bc) != -1;
                    if (!inAfterColliders && !hasExitedNormally)
                    {
                        logger.LogInfo("OnTriggerExit: Manually triggered on " + bc.name);
                        PlayerDirectVolumeOnTriggerExit.Invoke(pde, new object[] { bc });
                    }
                }
            }
        }
    }
}
