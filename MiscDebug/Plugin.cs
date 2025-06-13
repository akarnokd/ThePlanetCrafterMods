// Copyright (c) 2022-2025, David Karnok & Contributors
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
        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GroupsHandler), nameof(GroupsHandler.SetAllGroups))]
        static void GroupsHandler_SetAllGroups(List<Group> groups)
        {
            Dictionary<int, string> hashes = [];

            foreach (var g in groups)
            {
                var h = g.id.GetHashCode();
                h = (h ^ (h >>> 16)) & 2047;
                if (hashes.TryGetValue(h, out var s))
                {
                    logger.LogWarning("Collision: " + s + " <> " + g.id);
                }
                else
                {
                    hashes[h] = g.id;
                }
            }
        }
        */
    }
}
