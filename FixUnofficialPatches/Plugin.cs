// Copyright (c) 2022-2025, David Karnok & Contributors
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
using Steamworks;
using Unity.Netcode;
using System.Text;

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

        static readonly Color colorTransparent = new(0, 0, 0, 0);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DataTreatments), nameof(DataTreatments.ParseStringColor))]
        static void DataTreatments_ParseStringColor(ref string value)
        {
            value = value.Replace('/', '.');
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
        [HarmonyPatch(typeof(GamepadConfig), "OnDestroy")]
        static void GamepadConfig_OnDestroy(ref Callback<GamepadTextInputDismissed_t> ____gamepadTextInputDismissed)
        {
            ____gamepadTextInputDismissed ??= Callback<GamepadTextInputDismissed_t>.Create(
                    new Callback<GamepadTextInputDismissed_t>.DispatchDelegate(_ => { }));
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
        [HarmonyPatch(typeof(WorldUnitPositioning), nameof(WorldUnitPositioning.UpdateEvolutionPositioning))]
        static bool WorldUnitPositioning_UpdateEvolutionPositioning(TerraformStage ___startTerraformStage)
        {
            return ___startTerraformStage != null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnAutoForwardDispatcher))]
        static bool PlayerInputDispatcher_OnAutoForwardDispatcher(PlayerInputDispatcher __instance)
        {
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh != null)
            {
                var dialog = wh.GetOpenedUi();
                return dialog != DataConfig.UiType.Feedback
                    && dialog != DataConfig.UiType.TextInput
                    && dialog != DataConfig.UiType.Chat;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TextHelpers), nameof(TextHelpers.AddLineBreaksOnText))]
        static bool TextHelpers_AddLineBreaksOnText(
            string textToLineBreak,
            ref string __result)
        {
            if (textToLineBreak.Contains("<br>"))
            {
                __result = textToLineBreak;
            } 
            else
            {
                var sentences = textToLineBreak.Split(". ");
                var sb = new StringBuilder(textToLineBreak.Length + 8 * sentences.Length + 10);

                for (int i = 0; i < sentences.Length; i++)
                {
                    sb.Append(sentences[i]);
                    if (i < sentences.Length - 1)
                    {
                        sb.Append(". ");
                        if (i % 2 == 0)
                        {
                            sb.Append("<br><br>");
                        }
                    }
                }

                __result = sb.ToString();
            }
            return false;
        }

        static PlanetLoader planetLoaderCache;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnitsHandler), nameof(WorldUnitsHandler.GetPlanetUnits))]
        static bool WorldUnitsHandler_GetPlanetUnits(
                string planetId,
                Dictionary<int, WorldUnitsPlanet> ____planetUnits,
                ref WorldUnitsPlanet __result
            )
        {
            var pid = planetId;
            if (string.IsNullOrEmpty(pid))
            {
                if (planetLoaderCache == null)
                {
                    planetLoaderCache = Managers.GetManager<PlanetLoader>();
                }
                var currentPlanet = planetLoaderCache.GetCurrentPlanetData();
                if (currentPlanet != null)
                {
                    pid = currentPlanet.id;
                }
            }
            if (string.IsNullOrEmpty(pid))
            {
                __result = null;
            }
            else
            {
                ____planetUnits.TryGetValue(pid.GetStableHashCode(), out __result);
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JsonablesHelper), nameof(JsonablesHelper.JsonableToWorldObject))]
        static void JsonablesHelper_JsonableToWorldObject(JsonableWorldObject jsonableWorldObject)
        {
            jsonableWorldObject.grwth = Mathf.Clamp(jsonableWorldObject.grwth, 0, 100);
        }
    }
}
