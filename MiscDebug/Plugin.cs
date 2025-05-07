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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "PlaceAllWorldObjects")]
        static bool WorldObjectsHandler_PlaceAllWorldObjects(
            WorldObjectsHandler __instance,
            int currentPlanetHash,
            ref int ____currentPlanetHash,
            Dictionary<int, WorldObject> ____allWorldObjects,
            ref bool ____hasInitiatedAllObjects
            )
        {

            Dictionary<int, WorldObject>.KeyCollection keys = ____allWorldObjects.Keys;
            ____currentPlanetHash = currentPlanetHash;

            var sw = Stopwatch.StartNew();

            logger.LogInfo(string.Format("PlaceAllWorldObjects - begin | {0:0.000}ms", sw.Elapsed.TotalMilliseconds));

            HashSet<int> currentKeys = [.. keys];
            logger.LogInfo(string.Format("PlaceAllWorldObjects - keys copy | {0:0.000}ms", sw.Elapsed.TotalMilliseconds));
            sw.Restart();

            while (currentKeys.Count != 0)
            {
                foreach (var key in currentKeys)
                {
                    var worldObject = ____allWorldObjects[key];
                    if (worldObject.GetIsPlaced())
                    {
                        __instance.InstantiateWorldObject(worldObject, true, 0UL);
                    }
                }
                logger.LogInfo(string.Format("PlaceAllWorldObjects - loop current keys | {0:0.000}ms", sw.Elapsed.TotalMilliseconds));
                sw.Restart();

                HashSet<int> updatedKeys = [.. keys];
                logger.LogInfo(string.Format("PlaceAllWorldObjects - keys copy new | {0:0.000}ms", sw.Elapsed.TotalMilliseconds));
                sw.Restart();

                updatedKeys.RemoveWhere(currentKeys.Contains);
                logger.LogInfo(string.Format("PlaceAllWorldObjects - remove seen | {0:0.000}ms", sw.Elapsed.TotalMilliseconds));
                sw.Restart();

                currentKeys = updatedKeys;
                if (currentKeys.Count != 0)
                {
                    logger.LogInfo("PlaceAllWorldObjects - more world objects: " + currentKeys.Count);
                }
            }
            ____hasInitiatedAllObjects = true;
            logger.LogInfo(string.Format("PlaceAllWorldObjects - done | {0:0.000}ms", sw.Elapsed.TotalMilliseconds));

            return false;
        }

    }
}
