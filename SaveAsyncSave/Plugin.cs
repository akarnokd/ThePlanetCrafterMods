// Copyright (c) 2022-2025, David Karnok & Contributors
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
using UnityEngine;
using System.Text.RegularExpressions;
using System.IO;

namespace SaveAsyncSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveasyncsave", "(Save) Async Save", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static volatile int mainThreadId;

        static readonly object gate = new();

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

        static string GetWorldStateSaveFilePath(string _saveFileName)
        {
            return $"{Application.persistentDataPath}/{_saveFileName}.json";
        }

        static string CleanSaveStringOfVariables(string finalSaveString)
        {
            finalSaveString = Regex.Replace(finalSaveString, ",\"gameMode\":[0-9]", "");
            finalSaveString = Regex.Replace(finalSaveString, ",\"gameDyingConsequences\":[0-9]", "");
            finalSaveString = Regex.Replace(finalSaveString, ",\"gameStartLocation\":[0-9]", "");
            return finalSaveString;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JSONExport), "SaveStringsInFile")]
        static void SaveStringsInFile(
            string _saveFileName, 
            List<string> _saveStrings,
            char ___chunckDelimiter,
            in char ___listDelimiter
        )
        {
            string text = "";
            foreach (string _saveString in _saveStrings)
            {
                string text2 = "\r" + _saveString.Replace(___listDelimiter.ToString(), ___listDelimiter + "\n") + "\r";
                text = text + text2 + ___chunckDelimiter;
            }

            UnityEngine.Debug.Log("Saving in : " + GetWorldStateSaveFilePath(_saveFileName));
            text = text.Replace(",\"demandGrps\":\"\",\"supplyGrps\":\"\",\"priority\":0", "");
            text = text.Replace(",\"set\":0", "");
            text = text.Replace(",\"grwth\":0", "");
            text = text.Replace(",\"hunger\":0.0", "");
            text = text.Replace(",\"trtVal\":0", "");
            text = text.Replace(",\"trtInd\":0", "");
            text = text.Replace(",\"siIds\":\"\"", "");
            text = text.Replace(",\"pnls\":\"\"", "");
            text = text.Replace(",\"color\":\"\"", "");
            text = text.Replace(",\"text\":\"\"", "");
            text = text.Replace(",\"liGrps\":\"\"", "");
            text = text.Replace(",\"liId\":0", "");
            text = text.Replace(",\"pos\":\"0,0,0\"", "");
            text = text.Replace(",\"rot\":\"0,0,0,0\"", "");
            text = text.Replace(",\"planet\":0", "");
            text = text.Replace(",\"liPlanet\":0", "");
            text = CleanSaveStringOfVariables(text);
            File.WriteAllText(GetWorldStateSaveFilePath(_saveFileName), text);
        }
    }
}
