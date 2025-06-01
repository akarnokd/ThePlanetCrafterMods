// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using BepInEx.Logging;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using Unity.Netcode;
using HarmonyLib;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Text;
using System.Linq;

namespace SaveAsyncSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveasyncsave", "(Save) Async Save", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static int mainThreadId;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

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
                Task.Factory.StartNew(() =>
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
    }
}
