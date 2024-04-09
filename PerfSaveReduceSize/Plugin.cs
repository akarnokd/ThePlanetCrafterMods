// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;

namespace PerfSaveReduceSize
{
    [BepInPlugin("akarnokd.theplanetcraftermods.perfsavereducesize", "(Perf) Reduce Save Size", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JSONExport), "SaveStringsInFile")]
        static bool JSONExport_SaveStringsInFile(List<string> _saveStrings)
        {
            _saveStrings[2] = _saveStrings[2]
                .Replace(",\"liId\":0,\"liGrps\":\"\",\"pos\":\"0,0,0\",\"rot\":\"0,0,0,0\",\"wear\":0,\"pnls\":\"\",\"color\":\"\",\"text\":\"\",\"grwth\":0", "")
                .Replace(",\"hunger\":0.0", "");
            return true;
        }
    }
}
