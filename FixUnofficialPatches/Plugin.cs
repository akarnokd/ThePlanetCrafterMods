// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

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
using TMPro;
using System.Globalization;
using Steamworks;
using Unity.Netcode;

namespace FixUnofficialPatches
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunofficialpatches", "(Fix) Unofficial Patches", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            SaveFilesSelectorScrollFix();
        }

        static SaveFilesSelector saveFilesSelectorInstance;
        static FieldInfo saveFilesSelectorObjectsInList;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveFilesSelector), "Start")]
        static void SaveFilesSelector_Start(SaveFilesSelector __instance)
        {
            saveFilesSelectorInstance = __instance;
            saveFilesSelectorObjectsInList = AccessTools.Field(typeof(SaveFilesSelector), "_objectsInList");
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

        /*
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
        */

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
        [HarmonyPatch(typeof(UiDropDownAndHover), nameof(UiDropDownAndHover.GetIndexOfSelectedItem))]
        static bool UiDropDownAndHover_GetIndexOfSelectedItem(TMP_Dropdown ___dropdown, ref int __result)
        {
            if (___dropdown == null)
            {
                __result = -1;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Actionnable), "HandleHoverMaterial")]
        static bool Actionnable_HandleHoverMaterial()
        {
            return Managers.GetManager<VisualsResourcesHandler>() != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GamepadConfig), "OnDestroy")]
        static void GamepadConfig_OnDestroy(ref Callback<GamepadTextInputDismissed_t> ____gamepadTextInputDismissed)
        {
            ____gamepadTextInputDismissed ??= Callback<GamepadTextInputDismissed_t>.Create(
                    new Callback<GamepadTextInputDismissed_t>.DispatchDelegate(_ => { }));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventorySpawnContent), "OnConstructibleDestroyed")]
        static bool InventorySpawnContent_OnConstructibleDestroyed()
        {
            return NetworkManager.Singleton != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineOutsideGrower), "InstantiateAtRandomPosition")]
        static bool MachineOutsideGrower_InstantiateAtRandomPosition()
        {
            return NetworkManager.Singleton != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrower), "OnVegetableGrabed")]
        static bool MachineGrower_OnVegetableGrabed()
        {
            return InventoriesHandler.Instance != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "CreateNewTaskForWorldObjectForSpawnedObject")]
        static bool LogisticManager_CreateNewTaskForWorldObjectForSpawnedObject(WorldObject worldObject)
        {
            var go = worldObject.GetGameObject();
            if (go != null)
            {
                var ag = go.GetComponentInChildren<ActionGrabable>();
                if (ag != null)
                {
                    return !LibCommon.GrabChecker.IsOnDisplay(ag) && ag.GetCanGrab();
                }
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GroupNetworkBase), "DeconstructServerRpc")]
        static void GroupNetworkBase_DeconstructServerRpc(GroupNetworkBase __instance, 
            ref ActionDeconstructible ____actionDeconstruct)
        {
            NetworkManager networkManager = __instance.NetworkManager;
            if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
            {
                return;
            }
            if (____actionDeconstruct == null)
            {
                ____actionDeconstruct = __instance.GetComponentInChildren<ActionDeconstructible>(true);
            }
        }
    }
}
