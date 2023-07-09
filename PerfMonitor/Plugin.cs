using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Diagnostics;
using System.Collections;
using System;
using BepInEx.Configuration;

namespace PerfMonitor
{
    [BepInPlugin("akarnokd.theplanetcraftermods.perfmonitor", "(Perf) Monitor", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        static ConfigEntry<bool> modEnabled;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            modEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");

            if (modEnabled.Value)
            {
                Harmony.CreateAndPatchAll(typeof(Plugin));
            }
        }

        static readonly Dictionary<string, Stopwatch> stopWatches = new();
        static readonly Dictionary<string, long> ticks = new();
        static readonly Dictionary<string, long> invokes = new();
        static float lastTime = 0;
        static float delay = 10f;

        void Update()
        {
            if (modEnabled.Value)
            {
                var now = Time.time;
                if (now >= lastTime + delay)
                {
                    logger.LogInfo("Perf [" + (now - lastTime) + " s]");

                    lastTime = now;

                    int max = 1;
                    List<string> keyList = new();
                    foreach (var k in ticks.Keys)
                    {
                        max = Mathf.Max(max, k.Length);
                        keyList.Add(k);
                    }
                    keyList.Sort();

                    foreach (var kv in keyList)
                    {
                        var k = kv;
                        var v = ticks[k];
                        logger.LogInfo(string.Format("  {0,-" + max + "} = {1:0.000} ms ({2}), Per call = {3:0.000000} ms",
                            k, v / 10000f, invokes[k], v / 10000f / invokes[k]));
                    }
                    invokes.Clear();
                    ticks.Clear();
                }
            }
        }

        static void PerfBegin(string key)
        {
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
        [HarmonyPatch(typeof(SavedDataHandler), "LoadWorldObjectsData")]
        static void SavedDataHandler_LoadWorldObjectsData(List<JsonableWorldObject> _jsonableWorldObjects)
        {
            PerfBegin("SavedDataHandler_LoadWorldObjectsData");
            /*
            HashSet<string> ignoreSet = new()
            {
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
            };

            for (int i = _jsonableWorldObjects.Count - 1; i >= 0; i--)
            {
                var wo = _jsonableWorldObjects[i];
                if (ignoreSet.Contains(wo.gId))
                {
                    _jsonableWorldObjects.RemoveAt(i);
                }
            }
            * /
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "LoadWorldObjectsData")]
        static void SavedDataHandler_LoadWorldObjectsData_Post()
        {
            PerfBegin("SavedDataHandler_LoadWorldObjectsData");
        }
            
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AchievementsHandler), "CraftedVerifications")]
        static void AchievementsHandler_CraftedVerifications()
        {
            PerfBegin("AchievementsHandler_CraftedVerifications");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(AchievementsHandler), "CraftedVerifications")]
        static void AchievementsHandler_CraftedVerifications_Post()
        {
            PerfEnd("AchievementsHandler_CraftedVerifications");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnit), "SetIncreaseAndDecreaseForWorldObjects")]
        static void WorldUnit_SetIncreaseAndDecreaseForWorldObjects()
        {
            PerfBegin("WorldUnit_SetIncreaseAndDecreaseForWorldObjects");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldUnit), "SetIncreaseAndDecreaseForWorldObjects")]
        static void WorldUnit_SetIncreaseAndDecreaseForWorldObjects_Post()
        {
            PerfEnd("WorldUnit_SetIncreaseAndDecreaseForWorldObjects");
        }

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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGrower), "UpdateSize")]
        static void MachineGrower_UpdateSize(ref IEnumerator __result)
        {
            __result = new IEnumeratorInterceptor("MachineGrower_UpdateSize", __result);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static void MachineGenerator_GenerateAnObject()
        {
            PerfBegin("MachineGenerator_GenerateAnObject");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static void MachineGeenrator_GenerateAnObject_Post()
        {
            PerfEnd("MachineGenerator_GenerateAnObject");
        }
        */

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "SetItemsInRange")]
        static void MachineAutoCrafter_SetItemsInRange()
        {
            PerfBegin("MachineAutoCrafter_SetItemsInRange");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "SetItemsInRange")]
        static void MachineAutoCrafter_SetItemsInRange_Post()
        {
            PerfEnd("MachineAutoCrafter_SetItemsInRange");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "CraftIfPossible")]
        static void MachineAutoCrafter_CraftIfPossible()
        {
            PerfBegin("MachineAutoCrafter_CraftIfPossible");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "CraftIfPossible")]
        static void MachineAutoCrafter_CraftIfPossible_Post()
        {
            PerfEnd("MachineAutoCrafter_CraftIfPossible");
        }

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RequireEnergyHandler), "UpdateAllEnergyRequester")]
        static void RequireEnergyHandler_UpdateAllEnergyRequester()
        {
            PerfBegin("RequireEnergyHandler_UpdateAllEnergyRequester");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RequireEnergyHandler), "UpdateAllEnergyRequester")]
        static void RequireEnergyHandler_UpdateAllEnergyRequester_Post()
        {
            PerfEnd("RequireEnergyHandler_UpdateAllEnergyRequester");
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(AlertUnlockables), "CheckIfNewUnlockableUnlocked")]
        static void AlertUnlockables_CheckIfNewUnlockableUnlocked(ref IEnumerator __result)
        {
            __result = new IEnumeratorInterceptor("AlertUnlockables_CheckIfNewUnlockableUnlocked", __result);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UnlockingHandler), "GetUnitUnlockablesGroupSegment")]
        static void UnlockingHandler_GetUnitUnlockablesGroupSegment()
        {
            PerfBegin("UnlockingHandler_GetUnitUnlockablesGroupSegment");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UnlockingHandler), "GetUnitUnlockablesGroupSegment")]
        static void UnlockingHandler_GetUnitUnlockablesGroupSegment_Post()
        {
            PerfEnd("UnlockingHandler_GetUnitUnlockablesGroupSegment");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RequireEnergy), "ChangeComponentsStatuts")]
        static void RequireEnergy_ChangeComponentsStatuts()
        {
            PerfBegin("RequireEnergy_ChangeComponentsStatuts");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RequireEnergy), "ChangeComponentsStatuts")]
        static void RequireEnergy_ChangeComponentsStatuts_Post()
        {
            PerfEnd("RequireEnergy_ChangeComponentsStatuts");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RequireEnergy), "CheckEnergyStatus", new Type[] { typeof(bool) })]
        static void RequireEnergy_CheckEnergyStatus()
        {
            PerfBegin("RequireEnergy_CheckEnergyStatus");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(RequireEnergy), "CheckEnergyStatus", new Type[] { typeof(bool) })]
        static void RequireEnergy_CheckEnergyStatus_Post()
        {
            PerfEnd("RequireEnergy_CheckEnergyStatus");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WaterHandler), "IsUnderWater")]
        static void WaterHandler_IsUnderWater()
        {
            PerfBegin("WaterHandler_IsUnderWater");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WaterHandler), "IsUnderWater")]
        static void WaterHandler_IsUnderWater_Post()
        {
            PerfEnd("WaterHandler_IsUnderWater");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnitsHandler), nameof(WorldUnitsHandler.ForceResetAllValues))]
        static void WorldUnitsHandler_ForceResetAllValues()
        {
            PerfBegin("WorldUnitsHandler_ForceResetAllValues");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldUnitsHandler), nameof(WorldUnitsHandler.ForceResetAllValues))]
        static void WorldUnitsHandler_ForceResetAllValues_Post()
        {
            PerfEnd("WorldUnitsHandler_ForceResetAllValues");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.GetWorldObjectForInventory))]
        static void WorldObjectsHandler_GetWorldObjectForInventory()
        {
            PerfBegin("WorldObjectsHandler_GetWorldObjectForInventory");
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.GetWorldObjectForInventory))]
        static void WorldObjectsHandler_GetWorldObjectForInventory_Post()
        {
            PerfEnd("WorldObjectsHandler_GetWorldObjectForInventory");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "SetLogisticTasks")]
        static void LogisticManager_SetLogisticTasks(
            Dictionary<int, LogisticTask> ___allLogisticTasks,
            List<Drone> ___droneFleet,
            List<Inventory> ___supplyInventories,
            List<Inventory> ___demandInventories,
            List<MachineDroneStation> ___allDroneStations
        )
        {
            / *
            logger.LogInfo("LogisticManager_SetLogisticTasks\r\n"
                + "   droneFleet        = " + ___droneFleet.Count + "\r\n"
                + "   allDroneStations  = " + ___allDroneStations.Count + "\r\n"
                + "   allLogisticTasks  = " + ___allLogisticTasks.Count + "\r\n"
                + "   supplyInventories = " + ___supplyInventories.Count + "\r\n"
                + "   demandInventories = " + ___demandInventories.Count
            );
            * /
            PerfBegin("LogisticManager_SetLogisticTasks");
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogisticManager), "SetLogisticTasks")]
        static void LogisticManager_SetLogisticTasks_Post()
        {
            PerfEnd("LogisticManager_SetLogisticTasks");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetWorldObjectsOfGroup))]
        static void Inventory_GetWorldObjectsOfGroup()
        {
            PerfBegin("Inventory_GetWorldObjectsOfGroup");
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetWorldObjectsOfGroup))]
        static void Inventory_GetWorldObjectsOfGroup_Post()
        {
            PerfEnd("Inventory_GetWorldObjectsOfGroup");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
        static void Inventory_IsFull()
        {
            PerfBegin("Inventory_IsFull");
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
        static void Inventory_IsFull_Post()
        {
            PerfEnd("Inventory_IsFull");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), nameof(LogisticManager.GetANonAttributedTask))]
        static void LogisticManager_GetANonAttributedTask()
        {
            PerfBegin("LogisticManager_GetANonAttributedTask");
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogisticManager), nameof(LogisticManager.GetANonAttributedTask))]
        static void LogisticManager_GetANonAttributedTask_Post()
        {
            PerfEnd("LogisticManager_GetANonAttributedTask");
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDroneStation), nameof(MachineDroneStation.TryToReleaseOneDrone))]
        static void MachineDroneStation_TryToReleaseOneDrone()
        {
            PerfBegin("MachineDroneStation_TryToReleaseOneDrone");
        }
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineDroneStation), nameof(MachineDroneStation.TryToReleaseOneDrone))]
        static void MachineDroneStation_TryToReleaseOneDrone_Post()
        {
            PerfEnd("MachineDroneStation_TryToReleaseOneDrone");
        }

        */
        class IEnumeratorInterceptor : IEnumerator
        {
            readonly IEnumerator original;

            readonly string key;

            internal IEnumeratorInterceptor(string key, IEnumerator original)
            {
                this.original = original;
                this.key = key;
            }

            public object Current => original.Current;

            public bool MoveNext()
            {
                PerfBegin(key);
                try
                {
                    return original.MoveNext();
                }
                finally
                {
                    PerfEnd(key);
                }
            }

            public void Reset()
            {
                original.Reset();
            }
        }
    }
}
