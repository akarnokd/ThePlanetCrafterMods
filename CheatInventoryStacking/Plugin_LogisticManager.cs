using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {

        static readonly Dictionary<int, WorldObject> inventoryOwnerCache = [];
        static readonly List<int> taskKeysToRemove = [];
        static readonly List<LogisticStationDistanceToTask> stationDistancesCache = [];
        static readonly List<LogisticTask> nonAttributedTasksCache = [];

        static readonly Comparison<Inventory> CompareInventoryPriorityDesc =
            (x, y) => y.GetLogisticEntity().GetPriority().CompareTo(x.GetLogisticEntity().GetPriority());

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "SetLogisticTasks")]
        static bool Patch_LogisticManager_SetLogisticTasks(
            LogisticManager __instance,
            NetworkVariable<bool> ____hasLogisticsEnabled,
            Dictionary<int, LogisticTask> ____allLogisticTasks,
            List<MachineDroneStation> ____allDroneStations,
            List<Drone> ____droneFleet,
            List<Inventory> ____supplyInventories,
            List<Inventory> ____demandInventories
        )
        {
            var n = stackSize.Value;
            if (n <= 1)
            {
                return true;
            }

            UpdateInventoryOwnerCache();
            
            var pickables = WorldObjectsHandler.Instance.GetPickablesByDronesWorldObjects();
            ____demandInventories.Sort(CompareInventoryPriorityDesc);

            foreach (var demandInventory in ____demandInventories)
            {
                var demandInventorySize = demandInventory.GetSize();
                if (CanStack(demandInventory.GetId()))
                {
                    demandInventorySize *= n;
                }
                foreach (var demandGroup in demandInventory.GetLogisticEntity().GetDemandGroups())
                {
                    if (!IsFullStackedOfInventory(demandInventory, demandGroup.id))
                    {
                        foreach (var supplyInventory in ____supplyInventories)
                        {
                            if (demandInventory != supplyInventory)
                            {
                                foreach (var supplyGroup in supplyInventory.GetLogisticEntity().GetSupplyGroups())
                                {
                                    if (demandGroup == supplyGroup)
                                    {
                                        foreach (var supplyWo in supplyInventory.GetInsideWorldObjects())
                                        {
                                            if (supplyWo.GetGroup() == supplyGroup)
                                            {
                                                if (demandInventory.GetInsideWorldObjects().Count + demandInventory.GetLogisticEntity().waitingDemandSlots < demandInventorySize)
                                                {
                                                    CreateNewTaskForWorldObject(
                                                        supplyInventory, demandInventory, supplyWo, 
                                                        ____allLogisticTasks, inventoryOwnerCache);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var wo in pickables)
                        {
                            if (wo.GetGroup() == demandGroup
                                && wo.GetPosition() != Vector3.zero
                            )
                            {
                                var go = wo.GetGameObject();
                                if (go != null)
                                {
                                    var ag = go.GetComponentInChildren<ActionGrabable>();
                                    if (ag != null && ag.GetCanGrab())
                                    {
                                        if (demandInventory.GetInsideWorldObjects().Count + demandInventory.GetLogisticEntity().waitingDemandSlots < demandInventorySize)
                                        {
                                            CreateNewTaskForWorldObjectForSpawnedObject(
                                                demandInventory, wo, 
                                                ____allLogisticTasks, inventoryOwnerCache);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            taskKeysToRemove.Clear();
            nonAttributedTasksCache.Clear();
            foreach (var taskEntry in ____allLogisticTasks)
            {
                var key = taskEntry.Key;
                var task = taskEntry.Value;
                if (IsFullStackedOfInventory(task.GetDemandInventory(), 
                    task.GetWorldObjectToMove().GetGroup().id))
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
                    nonAttributedTasksCache.Add(task);
                }
            }

            foreach (var key in taskKeysToRemove)
            {
                ____allLogisticTasks.Remove(key);
            }

            int nextNonAttributedTaskIndex = 0;

            foreach (var drone in ____droneFleet)
            {
                if (nextNonAttributedTaskIndex >= nonAttributedTasksCache.Count)
                {
                    break;
                }
                if (drone.GetLogisticTask() == null)
                {
                    drone.SetLogisticTask(nonAttributedTasksCache[nextNonAttributedTaskIndex++]);
                }
            }

            for (int i = nextNonAttributedTaskIndex; i < nonAttributedTasksCache.Count; i++)
            {
                var task = nonAttributedTasksCache[i];
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
                    stationDistancesCache.Clear();

                    foreach (var station in ____allDroneStations)
                    {
                        var dist = Mathf.RoundToInt(Vector3.Distance(station.transform.position, supplyPosition));
                        stationDistancesCache.Add(new(station, dist)); 
                    }

                    stationDistancesCache.Sort();

                    foreach (var dist in stationDistancesCache)
                    {
                        var go = dist.GetMachineDroneStation().TryToReleaseOneDrone();
                        if (go != null && go.TryGetComponent<Drone>(out var drone))
                        {
                            drone.SetLogisticTask(task);
                            break;
                        }
                    }
                }
            }

            return false;
        }

        static void UpdateInventoryOwnerCache()
        {
            inventoryOwnerCache.Clear();
            foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                var iid = wo.GetLinkedInventoryId();
                if (iid != 0)
                {
                    inventoryOwnerCache[iid] = wo;
                }
            }
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "FindDemandForDroneOrDestroyContent")]
        static bool Patch_LogisticManager_FindDemandForDroneOrDestroyContent(
            Drone drone,
            List<Inventory> ____demandInventories,
            Dictionary<int, LogisticTask> ____allLogisticTasks
        )
        {
            var n = stackSize.Value;
            if (n <= 1)
            {
                return true;
            }

            var droneInv = drone.GetDroneInventory();
            var items = droneInv.GetInsideWorldObjects();

            if (items.Count == 0)
            {
                // We don't let the original method crash either...
                return false;
            }

            var wo = items[0];
            var gr = wo.GetGroup();
            var gid = gr.id;
            
            drone.SetLogisticTask(null);

            foreach (var inv in ____demandInventories)
            {
                foreach (var dg in inv.GetLogisticEntity().GetDemandGroups())
                {
                    if (dg == gr && !IsFullStackedOfInventory(inv, gid))
                    {
                        var task = CreateNewTaskForWorldObject(droneInv, inv, wo, ____allLogisticTasks);
                        if (task != null)
                        {
                            task.SetTaskState(LogisticData.TaskState.ToDemand);
                            drone.SetLogisticTask(task);
                            return false;
                        }
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
            Dictionary<int, LogisticTask> _allLogisticTasks,
            Dictionary<int, WorldObject> inventoryOwner = null
        )
        {
            if (_allLogisticTasks.ContainsKey(worldObject.GetId()))
            {
                return null;
            }

            WorldObject supplyWorldObject = null;
            WorldObject demandWorldObject = null;
            if (inventoryOwner != null)
            {
                inventoryOwner.TryGetValue(supplyInventory.GetId(), out supplyWorldObject);
                inventoryOwner.TryGetValue(demandInventory.GetId(), out demandWorldObject);
            }
            else
            {
                foreach (var wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
                {
                    if (wo.GetLinkedInventoryId() == supplyInventory.GetId())
                    {
                        supplyWorldObject = wo;
                    }
                    else
                    if (wo.GetLinkedInventoryId() == demandInventory.GetId())
                    {
                        demandWorldObject = wo;
                    }
                    if (supplyWorldObject != null && demandWorldObject != null)
                    {
                        break;
                    }

                }
            }
            if (supplyWorldObject == null || demandWorldObject == null)
            {
                return null;
            }

            var task = new LogisticTask(worldObject, supplyInventory, demandInventory, supplyWorldObject, demandWorldObject);
            _allLogisticTasks[worldObject.GetId()] = task;
            return task;
        }

        static LogisticTask CreateNewTaskForWorldObjectForSpawnedObject(
            Inventory demandInventory,
            WorldObject worldObject,
            Dictionary<int, LogisticTask> _allLogisticTasks,
            Dictionary<int, WorldObject> inventoryOwner)
        {
            if (_allLogisticTasks.ContainsKey(worldObject.GetId())
                || !inventoryOwner.TryGetValue(demandInventory.GetId(), out var demandWorldObject))
            {
                return null;
            }

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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), "SetClosestAvailableDroneStation")]
        static bool Patch_Drone_SetClosestAvailableDroneStation(
            Drone __instance,
            ref MachineDroneStation ____associatedDroneStation,
            GameObject ____droneRoot,
            WorldObject ____droneWorldObject,
            List<MachineDroneStation> allDroneStations
        )
        {
            if (stackSize.Value <= 1 || !stackDroneStation.Value)
            {
                return false;
            }

            var droneGroupId = ____droneWorldObject?.GetGroup().id;

            if (droneGroupId != null )
            {
                if (allDroneStations.Count == 1)
                {
                    var onlyDroneStation = allDroneStations[0];
                    var inv = onlyDroneStation.GetDroneStationInventory();
                    if (!IsFullStackedOfInventory(inv, droneGroupId))
                    {
                        ____associatedDroneStation = onlyDroneStation;
                        return false;
                    }
                }

                var maxDistance = float.MaxValue;
                var pos = ____droneRoot.transform.position;

                foreach (var ds in allDroneStations)
                {
                    var inv = ds.GetDroneStationInventory();
                    if (!IsFullStackedOfInventory(inv, droneGroupId))
                    {
                        var dist = Vector3.Distance(ds.gameObject.transform.position, pos);
                        if (dist < maxDistance)
                        {
                            maxDistance = dist;
                            ____associatedDroneStation = ds;
                        }
                    }
                }
            }

            if (____associatedDroneStation == null && allDroneStations.Count != 0)
            {
                ____associatedDroneStation = allDroneStations[UnityEngine.Random.Range(0, allDroneStations.Count)];
            }

            return false;
        }
    }
}
