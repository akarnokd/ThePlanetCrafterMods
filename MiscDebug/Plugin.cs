// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

            // LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetList), nameof(PlanetList.GetIsPlanetPurchased))]
        static bool PlanetList_GetIsPlanetPurchased(ref bool __result)
        {
            __result = true;
            return false;
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetList), "GetShowInBuild")]
        static bool PlanetList_GetShowInBuild(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
