﻿// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiscDebug
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscdebug", "(Misc) Debug", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static ConfigEntry<bool> coroutineTrace;
        static ConfigEntry<float> traceThreshold; 

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            logger = Logger;

            coroutineTrace = Config.Bind("General", "CoroutineTrace", false, "Enable tracing of coroutine execution times?");
            traceThreshold = Config.Bind("General", "TraceThreshold", 30f, "The threshold for reporting in slow sum coroutine execution times.");

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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.PlaceAllWorldObjects))]
        static void WorldObjectsHandler_PlaceAllWorldObjects(WorldObjectsHandler __instance)
        {
            HashSet<int> inventoryUsed = [];

            foreach (var wo in __instance.GetAllWorldObjects().Values)
            {
                if (wo.GetLinkedInventoryId() > 0)
                {
                    inventoryUsed.Add(wo.GetLinkedInventoryId());
                }
                var alt = wo.GetSecondaryInventoriesId();
                if (alt != null)
                {
                    foreach (var iid in alt)
                    {
                        inventoryUsed.Add(iid);
                    }
                }
            }

            foreach (var inv in InventoriesHandler.Instance.GetAllInventories().Values)
            {
                int id = inv.GetId();
                var le = inv.GetLogisticEntity();
                if (id < 100_000_000 && !inventoryUsed.Contains(id) && (le.GetSupplyGroups().Count != 0 || le.GetDemandGroups().Count != 0))
                {
                    logger.LogWarning("Inventory " + inv.GetId() + " no world object is using it");
                }
            }
        }
        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GroupsHandler), nameof(GroupsHandler.SetAllGroups))]
        static void GroupsHandler_SetAllGroups(List<Group> groups)
        {
            logger.LogInfo(System.Environment.StackTrace);
        }
        */

        static int lastFrame;
        static Dictionary<string, double> coroutineTimes = [];
        static int wip;
        static ConcurrentQueue<Dictionary<string, double>> queue = [];
        static bool once;
        static string path;
        static bool isEnabled;

        void Update()
        {
            if (Keyboard.current[Key.Period].wasPressedThisFrame)
            {
                isEnabled = !isEnabled;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MonoBehaviour), nameof(MonoBehaviour.StartCoroutine), [typeof(IEnumerator)])]
        static void MonoBehaviour_StartCoroutine(ref IEnumerator routine)
        {
            if (coroutineTrace.Value)
            {
                routine = DecoratedEnumerator(routine);
            }
        }

        static IEnumerator DecoratedEnumerator(IEnumerator original)
        {
            var sw = new Stopwatch();
            var str = original.GetType().FullName;
            for (; ; )
            {
                sw.Restart();

                bool result = original.MoveNext();

                var el = sw.Elapsed.TotalMilliseconds;

                int frameIndex = Time.frameCount;
                if (frameIndex != lastFrame)
                {
                    lastFrame = frameIndex;
                    var saved = coroutineTimes;
                    coroutineTimes = [];
                    if (isEnabled)
                    {
                        Save(saved);
                    }
                }

                coroutineTimes.TryGetValue(str, out var n);
                coroutineTimes[str] = n + el;

                if (!result)
                {
                    break;
                }

                yield return original.Current;
            }
        }

        static void Save(Dictionary<string, double> dict)
        {
            if (path == null)
            {
                path = Application.persistentDataPath;
            }
            queue.Enqueue(dict);
            if (Interlocked.Increment(ref wip) == 1)
            {
                Task.Factory.StartNew(SaveRoutine);
            }
        }

        static void SaveRoutine()
        {
            for (; ; )
            {
                if (queue.TryDequeue(out var e))
                {
                    var sum = e.Values.Sum();
                    if (!once)
                    {
                        File.Delete(path + "\\frametimes.csv");
                        once = true;
                    }
                    if (sum > traceThreshold.Value)
                    {
                        File.AppendAllLines(path + "\\frametimes.csv",
                            e
                            .Select(x => (x.Key, x.Value))
                            .OrderByDescending(x => x.Value)
                            .Select(k => k.Key + ";" + k.Value)
                            .Append(";" + sum)
                            .Append(";")
                            );
                    }
                }

                if (Interlocked.Decrement(ref wip) == 0)
                {
                    break;
                }
            }
        }
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(RequireEnergyHandler), nameof(RequireEnergyHandler.HasEnoughEnergy))]
        static void RequireEnergyHandler_HasEnoughEnergy(int planetHash)
        {
            if (planetHash != 0)
            {
                var p = Managers.GetManager<PlanetLoader>().planetList.GetPlanetFromIdHash(planetHash);
                WorldUnit unit = Managers.GetManager<WorldUnitsHandler>().GetUnit(DataConfig.WorldUnitType.Energy, p.GetPlanetId());
                if (unit == null)
                {
                    logger.LogError(planetHash + ": " + p.GetPlanetId());
                }
            }
        }
        */
    }
}
