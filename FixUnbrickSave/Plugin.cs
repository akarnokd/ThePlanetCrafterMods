using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Diagnostics;

namespace FixUnbrickSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunbricksave", "(Fix) Unbrick Save", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SavedDataHandler), "LoadWorldObjectsData")]
        static void SavedDataHandler_LoadWorldObjectsData(List<JsonableWorldObject> _jsonableWorldObjects)
        {
            HashSet<string> ignoreSet = new()
            {
                /*
                "Drill3",
                "Drill4",
                "Heater4",
                "Heater5",
                "EnergyGenerator6",
                "OreExtractor3",
                "Beehive1",
                "ButterflyDome1",
                "TreeSpreader2",
                "InsideLamp1",
                "ButterflyFarm1",
                "Biodome2",
                "Farm1",
                "AlgaeGenerator2",
                */
            };

            for (int i = _jsonableWorldObjects.Count - 1; i >= 0; i--)
            {
                var wo = _jsonableWorldObjects[i];
                if (ignoreSet.Contains(wo.gId))
                {
                    _jsonableWorldObjects.RemoveAt(i);
                }
            }
        }

        static int frameIndex = -1;
        static readonly Dictionary<string, Stopwatch> stopWatches = new();
        static readonly Dictionary<string, long> ticks = new();
        static readonly Dictionary<string, long> invokes = new();

        static void PerfBegin(string key)
        {
            var fc = Time.frameCount;
            if (frameIndex != fc)
            {
                logger.LogInfo("Perf [" + frameIndex + "]");
                
                foreach (var kv in ticks)
                {
                    logger.LogInfo(string.Format("  {0} = {1:0.000} ms ({2})", kv.Key, kv.Value / 10000f, invokes[kv.Key]));
                }
                invokes.Clear();
                ticks.Clear();

                frameIndex = fc;
            }
            if (!stopWatches.TryGetValue(key, out var sw))
            {
                sw = new();
                stopWatches[key] = sw;
            }
            invokes.TryGetValue(key, out var cnt);
            invokes[key] = cnt + 1;
            sw.Restart();
        }

        static void PerfEnd(string key)
        {
            var sw = stopWatches[key];
            ticks.TryGetValue(key, out var t);
            ticks[key] = t + sw.ElapsedTicks;
            sw.Stop();
        }

        /*

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AchievementsHandler), "CraftedVerifications")]
        static void AchievementsHandler_CraftedVerifications()
        {
            achievementsHandler_CraftedVerifications = new();
            achievementsHandler_CraftedVerifications.Start();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AchievementsHandler), "CraftedVerifications")]
        static void AchievementsHandler_CraftedVerifications_Post()
        {
            logger.LogInfo("AchievementsHandler_CraftedVerifications: " + (achievementsHandler_CraftedVerifications.ElapsedTicks / 10000f));
        }
        */
        /*
        static Stopwatch worldUnit_SetIncreaseAndDecreaseForWorldObjects;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnit), "SetIncreaseAndDecreaseForWorldObjects")]
        static void WorldUnit_SetIncreaseAndDecreaseForWorldObjects()
        {
            worldUnit_SetIncreaseAndDecreaseForWorldObjects = new();
            worldUnit_SetIncreaseAndDecreaseForWorldObjects.Start();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldUnit), "SetIncreaseAndDecreaseForWorldObjects")]
        static void WorldUnit_SetIncreaseAndDecreaseForWorldObjects_Post()
        {
            logger.LogInfo("WorldUnit_SetIncreaseAndDecreaseForWorldObjects: " + (worldUnit_SetIncreaseAndDecreaseForWorldObjects.ElapsedTicks / 10000f));
        }
        */

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineOutsideGrower), "Grow")]
        static void MachineOutsideGrower_Grow()
        {
            PerfBegin("MachineOutsideGrower_Grow");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOutsideGrower), "Grow")]
        static void MachineOutsideGrower_Grow_Post()
        {
            PerfEnd("MachineOutsideGrower_Grow");
        }
        */
    }
}
