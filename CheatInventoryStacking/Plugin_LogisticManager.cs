// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Jobs;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        static readonly List<int> taskKeysToRemove = [];
        static readonly List<LogisticStationDistanceToTask> stationDistancesCacheCurrentPlanet = [];
        static readonly Dictionary<int, List<LogisticTask>> nonAttributedTasksCacheAllPlanets = [];
        static readonly List<int> allTasksFrameCache = [];
        static readonly Dictionary<string, bool> inventoryGroupIsFull = [];
        static readonly List<Drone> droneFleetCache = [];

        static int pickablesVersion;
        static int pickablesCacheVersion = -1;
        static readonly List<WorldObject> pickablesCache = [];
        static readonly HashSet<string> pickablesGroupsCache = [];

        static readonly Comparison<Inventory> CompareInventoryPriorityDesc =
            (x, y) => y.GetLogisticEntity().GetPriority().CompareTo(x.GetLogisticEntity().GetPriority());

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "SetLogisticTasks")]
        static bool Patch_LogisticManager_SetLogisticTasks(
            LogisticManager __instance,
            Dictionary<int, LogisticTask> ____allLogisticTasks,
            HashSet<MachineDroneStation> ____allDroneStations,
            List<Drone> ____accessArrayCorrespondingDrones,
            List<Inventory> ____supplyInventories,
            List<Inventory> ____demandInventories,
            ref IEnumerator __result
        )
        {
            if (stackSize.Value > 1 || debugOverrideLogisticsAnyway.Value)
            {
                __result = LogisticManager_SetLogisticTasks_Override(
                    __instance,
                    ____allLogisticTasks,
                    ____allDroneStations,
                    ____accessArrayCorrespondingDrones,
                    ____supplyInventories,
                    ____demandInventories
                );

                return false;
            }
            return true;
        }

        static IEnumerator LogisticManager_SetLogisticTasks_Override(
            LogisticManager __instance,
            Dictionary<int, LogisticTask> ____allLogisticTasks,
            HashSet<MachineDroneStation> ____allDroneStations,
            List<Drone> ____accessArrayCorrespondingDrones,
            List<Inventory> ____supplyInventories,
            List<Inventory> ____demandInventories
        )
        {
            var n = stackSize.Value;
            if (n <= 1 && !debugOverrideLogisticsAnyway.Value)
            {
                yield break;
            }

            fLogisticManagerUpdatingLogisticTasks(__instance) = true;

            const string routineId = "CheatInventoryStacking::SetLogisticTasks";
            while (!CoroutineCoordinator.CanRun(routineId))
            {
                yield return null;
            }

            Log("LogisticManager::SetLogisticTasks Running.");

            Log("LogisticManager::CompleteDroneTransformUpdateJob Running.");

            mLogisticManagerCompleteDroneTransformUpdateJob.Invoke(__instance, []);

            var frametimeLimit = logisticsTimeLimit.Value / 1000d;
            var timer = Stopwatch.StartNew();
            var totalTimer = Stopwatch.StartNew();

            inventoryGroupIsFull.Clear();

            var frameSkipCount = 0;
            
            ICollection<WorldObject> pickables = pickablesCache;

            Log("  LogisticManager::SetLogisticTasks pickables " + pickables.Count);
            Log("  LogisticManager::SetLogisticTasks demand    " + ____demandInventories.Count);
            Log("  LogisticManager::SetLogisticTasks supply    " + ____supplyInventories.Count);

            var inventoryActualSupplyGroupsMain = new Dictionary<int, HashSet<string>>();

            for (int i = 0; i < ____demandInventories.Count; i++)
            {
                Inventory demandInventory = ____demandInventories[i];
                var demandInventorySize = demandInventory.GetSize();
                if (CanStack(demandInventory.GetId()))
                {
                    demandInventorySize *= n;
                }
                var demandCount = fInventoryWorldObjectsInInventory(demandInventory).Count;
                var demandLE = demandInventory.GetLogisticEntity();
                var supplyCounter = 0;

                // var sw1 = Stopwatch.StartNew();
                foreach (var demandGroup in demandLE.GetDemandGroups())
                {
                    var isFull = IsFullStackedOfInventory(demandInventory, demandGroup.id);
                    var fullKey = demandInventory.GetId() + " " + demandGroup.id;
                    inventoryGroupIsFull[fullKey] = isFull;

                    if (!isFull && demandLE.GetWorldObject() != null)
                    {
                        foreach (var supplyInventory in ____supplyInventories)
                        {
                            var supplyLE = supplyInventory.GetLogisticEntity();
                            if (demandInventory != supplyInventory
                                && supplyLE.GetWorldObject() != null
                                && supplyLE.GetPlanetHash() == demandLE.GetPlanetHash()
                                && supplyLE.GetSupplyGroups().Contains(demandGroup))
                            {
                                var isCached = inventoryActualSupplyGroupsMain.TryGetValue(supplyInventory.GetId(), out var hasGroups);

                                if (hasGroups == null || hasGroups.Contains(demandGroup.id))
                                {
                                    var supplyContents = fInventoryWorldObjectsInInventory(supplyInventory);
                                    foreach (var supplyWo in supplyContents)
                                    {
                                        supplyCounter++;
                                        if (supplyWo.GetGroup() == demandGroup)
                                        {
                                            if (demandCount + demandLE.waitingDemandSlots < demandInventorySize)
                                            {
                                                CreateNewTaskForWorldObject(
                                                    supplyInventory, demandInventory, supplyWo,
                                                    ____allLogisticTasks);
                                            }
                                        }
                                        if (hasGroups == null)
                                        {
                                            hasGroups = [];
                                            inventoryActualSupplyGroupsMain[supplyInventory.GetId()] = hasGroups;
                                        }
                                        if (!isCached)
                                        {
                                            hasGroups.Add(supplyWo.GetGroup().id);
                                        }
                                        if (isCached && demandCount + demandLE.waitingDemandSlots >= demandInventorySize)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }

                            if (demandCount + demandLE.waitingDemandSlots >= demandInventorySize)
                            {
                                break;
                            }
                        }

                        /*
                        var el = timer.Elapsed.TotalMilliseconds;
                        if (el > frametimeLimit)
                        {
                            Log("    LogisticManager::SetLogisticTasks timeout on supply loop. "
                                + el.ToString("0.000") + " ms, Curr: " + i + ", Count: " + ____demandInventories.Count + ", SupplyCount: " + supplyCounter);
                        }
                        */

                        var el = timer.Elapsed.TotalMilliseconds;
                        var doCachePickables = pickablesCacheVersion != pickablesVersion;
                        if (doCachePickables)
                        {
                            pickablesCache.Clear();
                            pickablesGroupsCache.Clear();
                            pickables = WorldObjectsHandler.Instance.GetPickablesByDronesWorldObjects();
                        }

                        if (doCachePickables || pickablesGroupsCache.Contains(demandGroup.id))
                        {
                            foreach (var wo in pickables)
                            {
                                supplyCounter++;
                                var isPlaced = wo.GetPosition() != Vector3.zero;
                                var gr = wo.GetGroup();
                                if (doCachePickables && isPlaced)
                                {
                                    pickablesCache.Add(wo);
                                    pickablesGroupsCache.Add(gr.id);
                                }
                                if (gr == demandGroup
                                    && isPlaced
                                    && wo.GetPlanetHash() == demandLE.GetPlanetHash()
                                    && !____allLogisticTasks.ContainsKey(wo.GetId())
                                )
                                {
                                    if (demandCount + demandLE.waitingDemandSlots < demandInventorySize)
                                    {
                                        var go = wo.GetGameObject();
                                        if (go != null)
                                        {
                                            var ag = go.GetComponentInChildren<ActionGrabable>();
                                            if (ag != null && !LibCommon.GrabChecker.IsOnDisplay(ag) && ag.GetCanGrab())
                                            {
                                                CreateNewTaskForWorldObjectForSpawnedObject(
                                                    demandInventory, wo,
                                                    ____allLogisticTasks);
                                            }
                                        }
                                    }
                                }
                                if (!doCachePickables && demandCount + demandLE.waitingDemandSlots >= demandInventorySize)
                                {
                                    break;
                                }
                            }

                            if (doCachePickables)
                            {
                                int beforeCount = pickables.Count;
                                int afterCount = pickablesCache.Count;
                                int oldVersion = pickablesCacheVersion;
                                pickablesCacheVersion = pickablesVersion;
                                pickables = pickablesCache;
                                Log("    LogisticManager::SetLogisticTasks pickables cached: " + beforeCount + " -> " + afterCount + " in " + (timer.Elapsed.TotalMilliseconds - el) + " [" + oldVersion + " -> " + pickablesVersion + " (" + (pickablesVersion - oldVersion) + ")]");
                            }
                        }
                        /*
                        el = timer.Elapsed.TotalMilliseconds;
                        if (el > frametimeLimit)
                        {
                            Log("    LogisticManager::SetLogisticTasks timeout on pickables loop. "
                                + el.ToString("0.000") + " ms, Curr: " + i + ", Count: " + ____demandInventories.Count + ", SupplyCount: " + supplyCounter);
                        }
                        */
                    }
                }

                var elaps0 = timer.Elapsed.TotalMilliseconds;
                if (elaps0 >= frametimeLimit)
                {
                    CoroutineCoordinator.Yield(routineId, elaps0);
                    do
                    {
                        yield return null;
                    } 
                    while (!CoroutineCoordinator.CanRun(routineId));

                    mLogisticManagerCompleteDroneTransformUpdateJob.Invoke(__instance, []);

                    timer.Restart();
                    frameSkipCount++;
                    inventoryActualSupplyGroupsMain.Clear();
                    Log("    LogisticManager::SetLogisticTasks Timeout on demand discovery. "
                        + elaps0.ToString("0.000") + " ms, Curr: " + i + ", Count: " + ____demandInventories.Count + ", SupplyCount: " + supplyCounter);
                }
            }

            Log("  LogisticManager::SetLogisticTasks Demand-Supply matching done. " + timer.Elapsed.TotalMilliseconds.ToString("0.000") + " ms");

            taskKeysToRemove.Clear();
            nonAttributedTasksCacheAllPlanets.Clear();
            allTasksFrameCache.Clear();
            allTasksFrameCache.AddRange(____allLogisticTasks.Keys);

            Dictionary<int, int> nextNonAttributedTaskIndices = [];

            foreach (var taskEntry in allTasksFrameCache)
            {
                var key = taskEntry;
                if (____allLogisticTasks.TryGetValue(key, out var task))
                {
                    var inv = task.GetDemandInventory();
                    var gr = task.GetWorldObjectToMove().GetGroup().GetId();
                    var fullKey = inv.GetId() + " " + gr;
                    if (!inventoryGroupIsFull.TryGetValue(fullKey, out var isFull))
                    {
                        isFull = IsFullStackedOfInventory(inv, gr);
                        inventoryGroupIsFull[fullKey] = isFull;
                    }
                    if (isFull)
                    {
                        task.SetTaskState(LogisticData.TaskState.Done);
                        taskKeysToRemove.Add(key);
                    }
                    else if (task.GetTaskState() == LogisticData.TaskState.Done)
                    {
                        taskKeysToRemove.Add(key);
                    }
                    else if (task.GetTaskState() == LogisticData.TaskState.NotAttributed)
                    {
                        var planetHash = task.GetPlanetHash();
                        if (!nonAttributedTasksCacheAllPlanets.TryGetValue(planetHash, out var perPlanetNonAttributedList))
                        {
                            perPlanetNonAttributedList = [];
                            nonAttributedTasksCacheAllPlanets[planetHash] = perPlanetNonAttributedList;
                            nextNonAttributedTaskIndices[planetHash] = 0;
                        }
                        perPlanetNonAttributedList.Add(task);
                    }
                }
                var elaps3 = timer.Elapsed.TotalMilliseconds;
                if (elaps3 >= frametimeLimit)
                {
                    CoroutineCoordinator.Yield(routineId, elaps3);
                    do
                    {
                        yield return null;
                    }
                    while (!CoroutineCoordinator.CanRun(routineId));
                    mLogisticManagerCompleteDroneTransformUpdateJob.Invoke(__instance, []);
                    timer.Restart();
                    frameSkipCount++;
                    Log("    LogisticManager::SetLogisticTasks timeout on chest fullness detection");
                }
            }

            Log("  LogisticManager::SetLogisticTasks Chest fullness detection done. " + timer.Elapsed.TotalMilliseconds.ToString("0.000") + " ms");

            foreach (var key in taskKeysToRemove)
            {
                ____allLogisticTasks.Remove(key);
            }

            Log("  LogisticManager::SetLogisticTasks Task removals done. " + timer.Elapsed.TotalMilliseconds.ToString("0.000") + " ms");

            droneFleetCache.Clear();
            mLogisticManagerCompleteDroneTransformUpdateJob.Invoke(__instance, []);
            ref TransformAccessArray ____accessArray = ref fLogisticManagerAccessArray(__instance);
            if (____accessArray.isCreated)
            {
                for (int i = 0; i < ____accessArray.length; i++)
                {
                    if (____accessArray[i] != null)
                    {
                        Drone drone = ____accessArrayCorrespondingDrones[i];
                        droneFleetCache.Add(drone);
                    }
                }
            }

            for (int i = 0; i < droneFleetCache.Count; i++)
            {
                var drone = droneFleetCache[i];
                if (drone != null && drone.GetLogisticTask() == null)
                {
                    var planetHash = drone.GetDronePlanetHash();
                    if (!nextNonAttributedTaskIndices.TryGetValue(planetHash, out var nextNonAttributedTaskIndex))
                    {
                        nextNonAttributedTaskIndex = 0;
                        nextNonAttributedTaskIndices[planetHash] = 0;
                    }
                    if (nonAttributedTasksCacheAllPlanets.TryGetValue(planetHash, out var nonAttributedTaskList))
                    {
                        if (nextNonAttributedTaskIndex < nonAttributedTaskList.Count)
                        {
                            var taskToAttribute = nonAttributedTaskList[nextNonAttributedTaskIndex];
                            nextNonAttributedTaskIndices[planetHash] = nextNonAttributedTaskIndex + 1;
                            drone.SetLogisticTask(taskToAttribute, ____allDroneStations);
                        }
                    }
                }

                var elaps4 = timer.Elapsed.TotalMilliseconds;
                if (elaps4 >= frametimeLimit)
                {
                    CoroutineCoordinator.Yield(routineId, elaps4);
                    do
                    {
                        yield return null;
                    }
                    while (!CoroutineCoordinator.CanRun(routineId));
                    mLogisticManagerCompleteDroneTransformUpdateJob.Invoke(__instance, []);
                    timer.Restart();
                    frameSkipCount++;
                    Log("    LogisticManager::SetLogisticTasks timeout active drones task assignments");
                }
            }

            Log("  LogisticManager::SetLogisticTasks Active drones task attribution done. " + timer.Elapsed.TotalMilliseconds.ToString("0.000") + " ms");

            // Log("    LogisticManager::SetLogisticTasks nextNonAttributedTaskIndices " + nextNonAttributedTaskIndices.Count);
            foreach (var planetAndIndex in nextNonAttributedTaskIndices)
            {
                var planetHash = planetAndIndex.Key;
                var nextNonAttributedTaskIndex = planetAndIndex.Value;
                // Log("      LogisticManager::SetLogisticTasks planet " + planetHash + " index " + nextNonAttributedTaskIndex);

                if (nonAttributedTasksCacheAllPlanets.TryGetValue(planetHash, out var nonAttributedTasksList))
                {
                    for (int i = nextNonAttributedTaskIndex; i < nonAttributedTasksList.Count; i++)
                    {
                        var task = nonAttributedTasksList[i];

                        if (task.GetTaskState() != LogisticData.TaskState.NotAttributed)
                        {
                            continue;
                        }

                        Vector3 supplyPosition = Vector3.zero;
                        if (task.GetIsSpawnedObject())
                        {
                            supplyPosition = task.GetWorldObjectToMove().GetPosition();
                        }
                        else
                        {
                            var supplyWo = task.GetSupplyInventoryWorldObject();
                            if (supplyWo != null)
                            {
                                supplyPosition = supplyWo.GetPosition();
                            }
                        }
                        if (supplyPosition != Vector3.zero)
                        {
                            stationDistancesCacheCurrentPlanet.Clear();

                            foreach (var station in ____allDroneStations)
                            {
                                if (station.GetPlanetHash() == planetHash)
                                {
                                    var dist = Mathf.RoundToInt(Vector3.Distance(station.transform.position, supplyPosition));
                                    stationDistancesCacheCurrentPlanet.Add(new(station, dist));
                                }
                            }

                            // Log("      LogisticManager::SetLogisticTasks station candidates: " + stationDistancesCacheCurrentPlanet.Count);

                            stationDistancesCacheCurrentPlanet.Sort();

                            foreach (var dist in stationDistancesCacheCurrentPlanet)
                            {
                                var go = dist.GetMachineDroneStation().TryToReleaseOneDrone();
                                // Log("      LogisticManager::SetLogisticTasks drone found: " + (go != null));
                                if (go != null && go.TryGetComponent<Drone>(out var drone))
                                {
                                    drone.SetLogisticTask(task, ____allDroneStations);
                                    break;
                                }
                            }
                        }
                        var elaps1 = timer.Elapsed.TotalMilliseconds;
                        if (elaps1 >= frametimeLimit)
                        {
                            CoroutineCoordinator.Yield(routineId, elaps1);
                            do
                            {
                                yield return null;
                            }
                            while (!CoroutineCoordinator.CanRun(routineId));
                            mLogisticManagerCompleteDroneTransformUpdateJob.Invoke(__instance, []);
                            timer.Restart();
                            frameSkipCount++;
                            Log("    LogisticManager::SetLogisticTasks timeout on new drone attribution: "
                                + elaps1.ToString("0.000") + " ms, Start: " + nextNonAttributedTaskIndex + ", Curr: " + i + ", Count: " + nonAttributedTasksList.Count);
                        }
                    }
                }

            }

            Log("  LogisticManager::SetLogisticTasks New drone attribution done.");

            if (frameSkipCount > 0)
            {
                Log("  LogisticManager::SetLogisticTasks frameSkips: " + frameSkipCount + " (~ " + (frametimeLimit * frameSkipCount).ToString("0.000") + " ms)");
            }

            Log("LogisticManager::SetLogisticTasks Done: " + totalTimer.Elapsed.TotalMilliseconds.ToString("0.000") + " ms");

            var elaps2 = timer.Elapsed.TotalMilliseconds;
            if (elaps2 >= frametimeLimit)
            {
                Log("      --- yield (final) --- " + elaps2);
                CoroutineCoordinator.Yield(routineId, elaps2);
                do
                {
                    yield return null;
                }
                while (!CoroutineCoordinator.CanRun(routineId));
            }

            fLogisticManagerUpdatingLogisticTasks(__instance) = false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "FindDemandForDroneOrDestroyContent")]
        static bool Patch_LogisticManager_FindDemandForDroneOrDestroyContent(
            Drone drone,
            List<Inventory> ____demandInventories,
            Dictionary<int, LogisticTask> ____allLogisticTasks,
            HashSet<MachineDroneStation> ____allDroneStations
        )
        {
            var n = stackSize.Value;
            if (n <= 1)
            {
                return true;
            }

            var droneInv = drone.GetDroneInventory();
            var items = fInventoryWorldObjectsInInventory(droneInv);

            if (items.Count == 0)
            {
                // We don't let the original method crash either...
                return false;
            }

            var wo = items[0];
            var gr = wo.GetGroup();
            var gid = gr.id;
            
            drone.SetLogisticTask(null, ____allDroneStations);

            foreach (var inv in ____demandInventories)
            {
                if (inv.GetLogisticEntity().GetDemandGroups().Contains(gr) && !IsFullStackedOfInventory(inv, gid))
                {
                    var task = CreateNewTaskForWorldObject(droneInv, inv, wo, ____allLogisticTasks);
                    if (task != null)
                    {
                        task.SetTaskState(LogisticData.TaskState.ToDemand);
                        drone.SetLogisticTask(task, ____allDroneStations);
                        return false;
                    }
                }
            }

            WorldObjectsHandler.Instance.DestroyWorldObject(wo);

            return false;
        }

        static LogisticTask CreateNewTaskForWorldObject(
            Inventory supplyInventory, 
            Inventory demandInventory, 
            WorldObject worldObject,
            Dictionary<int, LogisticTask> _allLogisticTasks
        )
        {
            if (_allLogisticTasks.ContainsKey(worldObject.GetId()))
            {
                return null;
            }

            WorldObject supplyWorldObject = supplyInventory.GetLogisticEntity().GetWorldObject();
            WorldObject demandWorldObject = demandInventory.GetLogisticEntity().GetWorldObject();
            if (supplyWorldObject == null || demandWorldObject == null)
            {
                return null;
            }

            if (Api_1_AllowBufferLogisticsTaskEx != null 
                && !Api_1_AllowBufferLogisticsTaskEx(supplyInventory, supplyWorldObject, demandInventory, demandWorldObject, worldObject))
            {
                return null;
            }

            var task = new LogisticTask(worldObject, supplyInventory, demandInventory, supplyWorldObject, demandWorldObject);
            _allLogisticTasks[worldObject.GetId()] = task;
            return task;
        }

        static bool OwnsInventory(WorldObject wo, int iid)
        {
            if (wo.GetLinkedInventoryId() == iid)
            {
                return true;
            }
            foreach (var iid2 in wo.GetSecondaryInventoriesId())
            {
                if (iid2 == iid)
                {
                    return true;
                }
            }
            return false;
        }

        static LogisticTask CreateNewTaskForWorldObjectForSpawnedObject(
            Inventory demandInventory,
            WorldObject worldObject,
            Dictionary<int, LogisticTask> _allLogisticTasks)
        {
            if (_allLogisticTasks.ContainsKey(worldObject.GetId()))
            {
                return null;
            }
            var demandWorldObject = demandInventory.GetLogisticEntity().GetWorldObject();
            if (demandWorldObject == null)
            {
                return null;
            }
            // Log("CreateNewTaskForWorldObjectForSpawnedObject: " + worldObject.GetId() + " (" + worldObject.GetGroup().GetId() + ")");
            var task = new LogisticTask(worldObject, null, demandInventory, null, demandWorldObject, _isSpawnedObject: true);
            _allLogisticTasks[worldObject.GetId()] = task;
            return task;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineDroneStation), nameof(MachineDroneStation.SetDroneStationInventory))]
        static void Patch_MachineDroneStation_SetDroneStationInventory(Inventory inventory)
        {
            if (!stackDroneStation.Value)
            {
                noStackingInventories.Add(inventory.GetId());
            }
        }

        static readonly List<MachineDroneStation> candidateDroneStationsPerPlanet = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), "SetClosestAvailableDroneStation")]
        static bool Patch_Drone_SetClosestAvailableDroneStation(
            Drone __instance,
            ref MachineDroneStation ____associatedDroneStation,
            GameObject ____droneRoot,
            WorldObject ____droneWorldObject,
            HashSet<MachineDroneStation> allDroneStations
        )
        {
            if (stackSize.Value <= 1 || !stackDroneStation.Value)
            {
                return true;
            }

            var droneGroupId = ____droneWorldObject?.GetGroup().id;

            if (droneGroupId != null)
            {
                var dronePlanet = __instance.GetDronePlanetHash();
                var maxDistance = float.MaxValue;
                var pos = ____droneRoot.transform.position;

                foreach (var ds in allDroneStations)
                {
                    var inv = ds.GetDroneStationInventory();
                    if (ds.GetPlanetHash() == dronePlanet && !IsFullStackedOfInventory(inv, droneGroupId))
                    {
                        candidateDroneStationsPerPlanet.Add(ds);
                        var dist = Vector3.Distance(ds.gameObject.transform.position, pos);
                        if (dist < maxDistance)
                        {
                            maxDistance = dist;
                            ____associatedDroneStation = ds;
                        }
                    }
                }

                if (____associatedDroneStation == null && candidateDroneStationsPerPlanet.Count != 0)
                {
                    var rng = UnityEngine.Random.Range(0, candidateDroneStationsPerPlanet.Count);
                    ____associatedDroneStation = candidateDroneStationsPerPlanet[rng];
                }
            }

            candidateDroneStationsPerPlanet.Clear();

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "AddToSpecificLists")]
        static void Patch_WorldObjectsHandler_AddToSpecificLists(
            ref int __state,
            HashSet<WorldObject> ____itemsPickablesWorldObjects)
        {
            __state = ____itemsPickablesWorldObjects.Count;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "AddToSpecificLists")]
        static void Patch_WorldObjectsHandler_AddToSpecificLists_Post(
            ref int __state,
            HashSet<WorldObject> ____itemsPickablesWorldObjects)
        {
            if (__state != ____itemsPickablesWorldObjects.Count)
            {
                pickablesVersion++;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "DestroyWorldObject", [typeof(WorldObject), typeof(bool)])]
        static void Patch_WorldObjectsHandler_DestroyWorldObject(
            ref int __state,
            HashSet<WorldObject> ____itemsPickablesWorldObjects)
        {
            __state = ____itemsPickablesWorldObjects.Count;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "DestroyWorldObject", [typeof(WorldObject), typeof(bool)])]
        static void Patch_WorldObjectsHandler_DestroyWorldObject_Post(
            ref int __state,
            HashSet<WorldObject> ____itemsPickablesWorldObjects)
        {
            if (__state != ____itemsPickablesWorldObjects.Count)
            {
                pickablesVersion++;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "DropOnFloorImplentation")]
        static void Patch_WorldObjectsHandler_DropOnFloorImplentation()
        {
            pickablesVersion++;
        }
    }
}
