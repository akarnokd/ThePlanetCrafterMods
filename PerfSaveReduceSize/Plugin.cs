using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;

namespace PerfSaveReduceSize
{
    [BepInPlugin("akarnokd.theplanetcraftermods.perfsavereducesize", "(Perf) Reduce Save Size", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JSONExport), "SaveStringsInFile")]
        static bool JSONExport_SaveStringsInFile(List<string> _saveStrings)
        {
            _saveStrings[2] = _saveStrings[2].Replace(",\"liId\":0,\"liGrps\":\"\",\"pos\":\"0,0,0\",\"rot\":\"0,0,0,0\",\"wear\":0,\"pnls\":\"\",\"color\":\"\",\"text\":\"\",\"grwth\":0", ""); ;
            return true;
        }
    }
}
