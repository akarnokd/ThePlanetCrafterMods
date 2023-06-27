using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using BepInEx.Logging;
using System;
using System.Diagnostics;
using System.Text;

namespace PerfStartup
{
    [BepInPlugin("akarnokd.theplanetcraftermods.perfstartup", "(Perf) Startup", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static bool modEnabled = true;

        static ManualLogSource logger;

        void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

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
            string _fileName,
            ref GameObject __result)
        {
            if (modEnabled)
            {
                var sw = new Stopwatch();
                sw.Start();
                logger.LogInfo("Loading metadata: " + _fileName);

                LoadMetadata(_fileName, out var modeLabel, out var savedDataTerraformUnit, out var savedDataInfosCorrupted);

                var gameObject = Instantiate(___prefabSaveDisplayer);

                gameObject.GetComponent<SaveFileDisplayer>().SetData(_fileName, savedDataTerraformUnit, __instance, savedDataInfosCorrupted, modeLabel);
                gameObject.transform.SetParent(___displayersContainer.transform);
                gameObject.transform.SetSiblingIndex(0);
                gameObject.transform.localScale = new Vector3(1f, 1f, 1f);

                __result = gameObject;

                logger.LogInfo("Loading metadata: " + _fileName + " took " + (sw.ElapsedTicks / 10000) + "ms");
                return false;
            }
            return true;
        }

        static void LoadMetadata(string fileName, out string modeLabel, out WorldUnit ti, out bool corrupt)
        {
            corrupt = false;
            ti = null;
            modeLabel = "";
            try
            {
                using (var sr = new StreamReader(Path.Combine(Application.persistentDataPath, fileName + ".json")))
                {
                    if (sr.ReadLine() == null)
                    {
                        throw new IOException("File is empty: " + fileName);
                    }
                    var tiLine = sr.ReadLine();
                    if (tiLine == null)
                    {
                        throw new IOException("File does not have the Ti information: " + fileName);
                    }
                    var ws = new JsonableWorldState();
                    JsonUtility.FromJsonOverwrite(tiLine, ws);

                    ti = new WorldUnit(ws.unitHeatLevel + ws.unitPressureLevel + ws.unitOxygenLevel
                            + ws.unitPlantsLevel + ws.unitInsectsLevel + ws.unitAnimalsLevel, 
                        new List<string> { "Ti", "kTi", "MTi", "GTi", "TTi", "PTi", "ETi", "ZTi", "YTi" }, 
                        DataConfig.WorldUnitType.Terraformation);
                    ti.SetCurrentLabelIndex();

                    // now skip over 7 @ sections
                    int sections = 7;
                    for (; ; )
                    {
                        var line = sr.ReadLine();
                        if (line == null)
                        {
                            throw new IOException("File ends before the mode section: " + fileName);
                        }
                        if (line.StartsWith("@"))
                        {
                            if (--sections == 0)
                            {
                                line = sr.ReadLine();
                                if (line == null)
                                {
                                    throw new IOException("File ends just before the mode section: " + fileName);
                                }
                                var m = new JsonableGameState();
                                JsonUtility.FromJsonOverwrite(line, m);
                                modeLabel = Readable.GetModeLabel((DataConfig.GameSettingMode)Enum.Parse(typeof(DataConfig.GameSettingMode), m.mode));
                                break;
                            }
                        }
                    }

                    if (sections != 0)
                    {
                        throw new IOException("File ends with missing sections" + fileName + " (" + sections + " to go)");
                    }
                }
            } 
            catch (Exception ex)
            {
                logger.LogError(ex);
                corrupt = true;
            }
        }
    }
}
