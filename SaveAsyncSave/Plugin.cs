// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Text;
using System.Linq;
using UnityEngine;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;
using LibCommon;
using Unity.Netcode;
using static UnityEngine.UIElements.UxmlAttributeDescription;

namespace SaveAsyncSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveasyncsave", "(Save) Async Save", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static Plugin me;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static volatile int mainThreadId;

        static readonly object gate = new();

        static Task saveTask;

        static bool quitDelayOnce;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            me = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging (chatty!)");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void Log(string message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JSONExport), nameof(JSONExport.SaveToJson))]
        static bool JSONExport_SaveToJson(
            string _saveFileName,
            JsonableWorldState _worldState,
            List<JsonablePlanetState> _planetState,
            List<JsonablePlayerState> _playerState,
            JsonablePlayerStats _playerStats,
            JsonableGameState _gameSate,
            List<JsonableWorldObject> _worldObjects,
            List<JsonableInventory> _inventories,
            List<JsonableMessage> _messages,
            List<JsonableStoryEvent> _storyEvents,
            List<JsonableTerrainLayer> _terrainLayers,
            List<JsonableProceduralInstance> _proceduralInstances,
            ref Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
            if (!modEnabled.Value)
            {
                return true;
            }
            if (mainThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                saveTask = Task.Factory.StartNew(() =>
                {
                    lock (gate)
                    {
                        try
                        {
                            JSONExport.SaveToJson(
                                _saveFileName,
                                _worldState,
                                _planetState,
                                _playerState,
                                _playerStats,
                                _gameSate,
                                _worldObjects,
                                _inventories,
                                _messages,
                                _storyEvents,
                                _terrainLayers,
                                _proceduralInstances
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex);
                        }
                    }
                });
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(JSONExport), nameof(JSONExport.SaveToJson))]
        static void JSONExport_SaveToJson_Post(Stopwatch __state)
        {
            Log(Thread.CurrentThread.ManagedThreadId + ": " + __state.Elapsed.TotalMilliseconds);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetWorldObjects")]
        static bool SavedDataHandler_SetAndGetWorldObjects(
            ref List<JsonableWorldObject> __result,
            ref Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
            if (!modEnabled.Value)
            {
                return true;
            }

            var worldObjects = WorldObjectsHandler.Instance.GetAllWorldObjects();
            __result = new List<JsonableWorldObject>(worldObjects.Count + 1);

            foreach (var wo in worldObjects)
            {
                if (!wo.Value.GetDontSaveMe())
                {
                    __result.Add(JsonablesHelper.WorldObjectToJsonable(wo.Value));
                }
            }

            return false;
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetWorldObjects")]
        static void SavedDataHandler_SetAndGetWorldObjects_Post(
            ref Stopwatch __state)
        {
            Log("SavedDataHandler_SetAndGetWorldObjects: " + __state.Elapsed.TotalMilliseconds);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetInventories")]
        static void SavedDataHandler_SetAndGetInventories(ref Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetInventories")]
        static void SavedDataHandler_SetAndGetInventories_Post(ref Stopwatch __state)
        {
            Log("SavedDataHandler_SetAndGetInventories: " + __state.Elapsed.TotalMilliseconds);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            Application.wantsToQuit += WantsToQuit;
        }

        static bool WantsToQuit()
        {
            if (saveTask == null || saveTask.IsCompleted)
            {
                quitDelayOnce = false;
                return true;
            }
            Log("Can't just quit now.");
            if (!quitDelayOnce)
            {
                quitDelayOnce = true;
                me.StartCoroutine(SaveWaiter());
            }
            return false;
        }

        static IEnumerator SaveWaiter()
        {
            int n = 0;
            yield return new WaitForSecondsRealtime(1);
            while (saveTask != null && !saveTask.IsCompleted)
            {
                Log("Waiting for the save to complete...");
                if (++n == 1)
                {
                    MainMenuMessage.ShowDialog("Saving in progress...\n\nGame will close afterwards.");
                }
                yield return new WaitForSecondsRealtime(1);
            }
            yield return new WaitForEndOfFrame();
            quitDelayOnce = false;
            Log("Quitting.");
            Application.Quit();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "SetItemsInRange")]
        static bool MachineAutoCrafter_SetItemsInRange()
        {
            return !quitDelayOnce;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventorySpawnContent), "UpdateComponent")]
        static bool InventorySpawnContent_UpdateComponent()
        {
            return !quitDelayOnce;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkManager), "OnApplicationQuit")]
        static bool NetworkManager_OnApplicationQuit()
        {
            // Log(Environment.StackTrace);
            return !quitDelayOnce;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkUtils), "OnDestroy")]
        static bool NetworkUtils_OnDestroy()
        {
            return !quitDelayOnce;
        }
    }
}
