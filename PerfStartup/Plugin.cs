// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.IO;
using BepInEx.Logging;
using System;
using System.Diagnostics;
using BepInEx.Configuration;
using System.Collections.Generic;

namespace PerfStartup
{
    [BepInPlugin("akarnokd.theplanetcraftermods.perfstartup", "(Perf) Startup", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<bool> modEnabled;

        static ManualLogSource logger;

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static Stopwatch sw0;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.RefreshList))]
        static void SaveFilesSelector_RefreshList()
        {
            sw0 = Stopwatch.StartNew();
            logger.LogInfo("Begin RefresList");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.RefreshList))]
        static void SaveFilesSelector_RefreshList_Post()
        {
            logger.LogInfo("End RefresList: " + (sw0.ElapsedTicks / 10000) + " ms");
            sw0 = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveFilesSelector), "AddSaveToList")]
        static bool SaveFilesSelector_AddSaveToList(
            SaveFilesSelector __instance,
            GameObject ___prefabSaveDisplayer, 
            GameObject ___displayersContainer, 
            string fileName,
            ref GameObject __result)
        {
            if (modEnabled.Value)
            {
                var sw = new Stopwatch();
                sw.Start();
                logger.LogInfo("Loading metadata: " + fileName);

                LoadMetadata(fileName, 
                    out var modeLabel, 
                    out var savedDataTerraformUnit, 
                    out var savedDataInfosCorrupted,
                    out var state);

                var gameObject = Instantiate(___prefabSaveDisplayer);

                gameObject.GetComponent<SaveFileDisplayer>().SetData(
                    fileName, state, savedDataTerraformUnit, __instance, savedDataInfosCorrupted, modeLabel, 
                    state?.planetId ?? "Prime");
                gameObject.transform.SetParent(___displayersContainer.transform);
                gameObject.transform.SetSiblingIndex(0);
                gameObject.transform.localScale = new Vector3(1f, 1f, 1f);

                __result = gameObject;

                logger.LogInfo("Loading metadata: " + fileName + " took " + (sw.ElapsedTicks / 10000) + "ms");
                return false;
            }
            return true;
        }

        static void LoadMetadata(string fileName, out string modeLabel, out JsonablePlanetState ws, 
            out bool corrupt, out JsonableGameState state)
        {
            corrupt = false;
            ws = null;
            modeLabel = "";
            state = ScriptableObject.CreateInstance<JsonableGameState>();
            try
            {
                // Note: adding buffer size doesn't seem to help at all
                using var sr = new StreamReader(Path.Combine(Application.persistentDataPath, fileName + ".json"));
                
                if (sr.ReadLine() == null)
                {
                    throw new IOException("File is empty: " + fileName);
                }
                var tiLine = sr.ReadLine() ?? throw new IOException("File does not have the Ti information: " + fileName);


                ws = new JsonablePlanetState();
                tiLine = tiLine.Replace("unitBiomassLevel", "unitPlantsLevel");

                var isOldFileFormat = tiLine.Contains("unitOxygenLevel");

                int section = 1;
                if (isOldFileFormat)
                {
                    JsonUtility.FromJsonOverwrite(tiLine, ws);
                }

                // now skip over several @ sections till the custom save name info
                for (; ; )
                {
                    var line = sr.ReadLine();
                    if (line == null)
                    {
                        modeLabel = Readable.GetModeLabel(DataConfig.GameSettingMode.Standard);
                        state.saveDisplayName = "";
                        state.gameMode = DataConfig.GameSettingMode.Standard;
                        state.gameStartLocation = "Standard";
                        state.gameDyingConsequences = DataConfig.GameSettingDyingConsequences.DropSomeItems;
                        state.dyingConsequencesLabel = "DropSomeItems";
                        state.startLocationLabel = "Standard";
                        state.preInterplanetarySave = true;
                        break;
                    }
                    if (line.StartsWith("@", StringComparison.Ordinal))
                    {
                        section++;
                    } 
                    else 
                    { 
                        if (!isOldFileFormat && section == 2)
                        {
                            JsonablePlanetState wsTemp = new();
                            JsonUtility.FromJsonOverwrite(
                                line.Replace("unitBiomassLevel", "unitPlantsLevel")
                                .Replace("}|", "}"), 
                                wsTemp);

                            AddPlanetStates(ws, wsTemp);
                        }
                        else
                        if (section == (isOldFileFormat ? 8 : 9))
                        {
                            JsonUtility.FromJsonOverwrite(line, state);
                            state.preInterplanetarySave = isOldFileFormat;
                            modeLabel = Readable.GetModeLabel((DataConfig.GameSettingMode)Enum.Parse(typeof(DataConfig.GameSettingMode), state.mode));
                            break;
                        }
                    }
                }
                if (string.IsNullOrWhiteSpace(state.planetId))
                {
                    state.planetId = "Prime";
                }
            } 
            catch (Exception ex)
            {
                logger.LogError(ex);
                corrupt = true;
            }
        }

        static void AddPlanetStates(JsonablePlanetState dest, JsonablePlanetState src)
        {
            dest.unitOxygenLevel += src.unitOxygenLevel;
            dest.unitHeatLevel += src.unitHeatLevel;
            dest.unitPressureLevel += src.unitPressureLevel;
            dest.unitPlantsLevel += src.unitPlantsLevel;
            dest.unitInsectsLevel += src.unitInsectsLevel;
            dest.unitAnimalsLevel += src.unitAnimalsLevel;
            dest.unitPurificationLevel += src.unitPurificationLevel;
        }

    }
}
