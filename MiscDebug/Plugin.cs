// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System.Diagnostics;

namespace MiscDebug
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscdebug", "(Misc) Debug", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            logger = Logger;

            //Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProceduralInstancesHandler), nameof(ProceduralInstancesHandler.DeserializeInstances))]
        static void ProceduralInstancesHandler_DeserializeInstances_Pre(out Stopwatch __state)
        {
            __state = new Stopwatch();
            __state.Start();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ProceduralInstancesHandler), nameof(ProceduralInstancesHandler.DeserializeInstances))]
        static void ProceduralInstancesHandler_DeserializeInstances_Post(in Stopwatch __state)
        {
            var time = __state.ElapsedTicks / 10000;
            logger.LogInfo("ProceduralInstancesHandler::DeserializeInstances: " + string.Format("{0:#,##0.00} ms", time));
        }
    }
}
