using BepInEx;
using FeatMultiplayer.MessageTypes;
using HarmonyLib;
using SpaceCraft;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static readonly Dictionary<int, Vector3> droneTargetCache = new();

        static int droneSupplyCount;

        static int droneDemandCount;

        /// <summary>
        /// The vanilla routine assigns tasks to drones periodically.
        /// 
        /// On the client, we don't let it do and rely on signals for
        /// drone positions instead.
        /// </summary>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "SetLogisticTasks")]
        static bool LogisticManager_SetLogisticTasks()
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// The vanilla animates the drone position via this method.
        /// 
        /// We let the clients know if the target position changed since the last
        /// cached information.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___droneWorldObject"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), "MoveToTarget")]
        static void Drone_MoveToTarget(Drone __instance, WorldObject ___droneWorldObject, Vector3 _targetPosition)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (___droneWorldObject != null)
                {
                    bool targetChanged = false;
                    var id = ___droneWorldObject.GetId();

                    if (droneTargetCache.TryGetValue(id, out var tpos))
                    {
                        if (Vector3.Distance(tpos, _targetPosition) > 0.1)
                        {
                            targetChanged = true;
                            droneTargetCache[id] = _targetPosition;
                        }
                    }
                    else
                    {
                        droneTargetCache[id] = _targetPosition;
                        targetChanged = true;
                    }

                    if (targetChanged)
                    {
                        SendWorldObjectToClients(___droneWorldObject, false);

                        var msg = new MessageDronePosition();
                        msg.id = ___droneWorldObject.GetId();
                        msg.position = _targetPosition;
                        SendAllClients(msg);
                    }
                }
            }
        }

        /// <summary>
        /// The vanilla animates the drone position via this method.
        /// 
        /// We save the drone's position into the world object to minimize jitter.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___droneWorldObject"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Drone), "MoveToTarget")]
        static void Drone_MoveToTarget_Post(Drone __instance, WorldObject ___droneWorldObject, Vector3 _targetPosition)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (___droneWorldObject != null)
                {
                    ___droneWorldObject.SetPositionAndRotation(__instance.gameObject.transform.position, __instance.gameObject.transform.rotation);
                }
            }
        }

        /// <summary>
        /// This manages the drone state and movement, given a task.
        /// 
        /// On the client, we do nothing because we do not handle tasks
        /// and the vanilla behavior would just send the drone to a depot.
        /// </summary>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), nameof(Drone.UpdateState))]
        static bool Drone_UpdateState(Drone __instance, 
            MachineDroneStation ___associatedDroneStation, 
            List<MachineDroneStation> _allDroneStations)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (___associatedDroneStation == null)
                {
                    droneSetClosestAvailableDroneStation.Invoke(__instance, new object[] { _allDroneStations });
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Called when the drone enters the drone station.
        /// 
        /// On the client, we do nothing because we do not handle tasks
        /// and the vanilla behavior would just send the drone to a depot.
        /// </summary>
        /// <returns></returns>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Drone), "OnDestroy")]
        static void Drone_OnDestroy(WorldObject ___droneWorldObject, MachineDroneStation ___associatedDroneStation)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (___droneWorldObject != null)
                {
                    ___droneWorldObject.ResetPositionAndRotation();
                    SendWorldObjectToClients(___droneWorldObject, true);
                    droneTargetCache.Remove(___droneWorldObject.GetId());

                    LogInfo("Drone " + ___droneWorldObject?.GetId() + " hidden");
                }
                if (___associatedDroneStation.soundOnEnter != null)
                {
                    ___associatedDroneStation?.OnDroneEnter();
                }
            }
        }

        /// <summary>
        /// The vanilla code grabs the world object or takes the item out of the target inventory.
        /// 
        /// On the host, we have to immediately update clients when the world object grab happens
        /// as the game doesn't use grab, but destroys the rendered object.
        /// </summary>
        /// <param name="___logisticTask"></param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), "DroneLoad")]
        static void Drone_DroneLoad(LogisticTask ___logisticTask)
        {
            if (updateMode == MultiplayerMode.CoopHost && ___logisticTask != null && ___logisticTask.GetIsSpawnedObject())
            {
                SendWorldObjectToClients(___logisticTask.GetWorldObjectToMove(), false);
            }
        }

        /// <summary>
        /// This evicts drones from a station periodically.
        /// 
        /// On the client, we do nothing.
        /// 
        /// On the host, we need to capture the WorldObject 
        /// after to immediately show it on the client.
        /// </summary>
        /// <param name="__result"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDroneStation), nameof(MachineDroneStation.TryToReleaseOneDrone))]
        static bool MachineDroneStation_TryToReleaseOneDrone_Pre(ref GameObject __result)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                __result = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// This evicts drones from a station periodically.
        /// 
        /// On the client, we do nothing.
        /// 
        /// On the host, we need to capture the WorldObject 
        /// after to immediately show it on the client.
        /// </summary>
        /// <param name="__result"></param>
        /// <returns></returns>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineDroneStation), nameof(MachineDroneStation.TryToReleaseOneDrone))]
        static void MachineDroneStation_TryToReleaseOneDrone_Post(ref GameObject __result)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var wo = __result?.GetComponent<WorldObjectAssociated>()?.GetWorldObject();
                if (wo != null)
                {
                    SendWorldObjectToClients(wo, false);
                }
            }
        }

        /// <summary>
        /// Invoked in vanilla when the logistic entity has been changed on the GUI.
        /// 
        /// In multiplayer, we'll send an update message to everyone else.
        /// </summary>
        /// <param name="___logisticEntity"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogisticSelector), "ResetListAndInvokeEvent")]
        static void LogisticSelector_ResetListAndInvokeEvent(LogisticEntity ___logisticEntity)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                int inventoryId = -1;
                bool found = false;

                foreach (var inv in InventoriesHandler.GetAllInventories())
                {
                    if (inv.GetLogisticEntity() == ___logisticEntity)
                    {
                        inventoryId = inv.GetId();
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    var msg = new MessageUpdateSupplyDemand();
                    msg.inventoryId = inventoryId;
                    msg.priority = ___logisticEntity.GetPriority();
                    var dg = ___logisticEntity.GetDemandGroups();
                    if (dg != null)
                    {
                        msg.demandGroups.AddRange(dg.Select(v => v.id));
                    }
                    var sg = ___logisticEntity.GetSupplyGroups();
                    if (sg != null)
                    {
                        msg.supplyGroups.AddRange(sg.Select(v => v.id));
                    }
                    if (updateMode == MultiplayerMode.CoopHost)
                    {
                        SendAllClients(msg, true);
                    }
                    else
                    {
                        SendHost(msg, true);
                    }
                }
                else
                {
                    LogWarning("Could not determine whose LogisticEntity it is in this LogisticSelector.");
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogisticSelector), nameof(LogisticSelector.OnChangePriority))]
        static void LogisticSelector_OnChangePriority(LogisticEntity ___logisticEntity)
        {
            LogisticSelector_ResetListAndInvokeEvent(___logisticEntity);
        }

        static void ReceiveMessageUpdateSupplyDemand(MessageUpdateSupplyDemand msg)
        {
            Inventory inv = InventoriesHandler.GetInventoryById(msg.inventoryId);

            if (inv != null)
            {
                UpdateLogisticEntityFromMessage(inv, msg.demandGroups, msg.supplyGroups, msg.priority);

                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendAllClients(msg, true);
                }
            }
            else
            {
                LogWarning("ReceiveMessageUpdateSupplyDemand: Inventory " + msg.inventoryId + " not found");
            }
        }

        static void ReceiveMessageDronePosition(MessageDronePosition msg)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }
            if (worldObjectById.TryGetValue(msg.id, out var drone))
            {
                var go = drone.GetGameObject();
                if (go != null)
                {
                    var ds = go.GetComponent<DroneSmoother>();
                    if (ds == null)
                    {
                        ds = go.AddComponent<DroneSmoother>();
                        ds.drone = go.GetComponent<Drone>();
                    }

                    ds.targetPosition = msg.position;

                    /*
                    go.transform.position = msg.position;
                    go.transform.rotation = msg.rotation;
                    */
                }
            }
        }

        internal static void UpdateLogisticEntityFromMessage(Inventory inv, 
            List<string> demandGroups, List<string> supplyGroups, int priority)
        {
            var le = inv.GetLogisticEntity();

            {
                var dg = new List<Group>();
                foreach (var dgm in demandGroups)
                {
                    var g = GroupsHandler.GetGroupViaId(dgm);
                    if (g != null)
                    {
                        dg.Add(g);
                    }
                    else
                    {
                        LogWarning("UpdateLogisticEntityFromMessage: Inventory " + inv.GetId() + " unknown demand group " + dgm);
                    }
                }

                if (dg.Count != 0)
                {
                    le.SetDemandGroups(dg);
                }
                else
                {
                    le.SetDemandGroups(null);
                }
            }

            {
                var sg = new List<Group>();
                foreach (var sgm in supplyGroups)
                {
                    var g = GroupsHandler.GetGroupViaId(sgm);
                    if (g != null)
                    {
                        sg.Add(g);
                    }
                    else
                    {
                        LogWarning("UpdateLogisticEntityFromMessage: Inventory " + inv.GetId() + " unknown supply group " + sgm);
                    }
                }

                if (sg.Count != 0)
                {
                    le.SetSupplyGroups(sg);
                }
                else
                {
                    le.SetSupplyGroups(null);
                }
            }
            le.SetPriority(priority);

            // if the player is looking at it right now.
            var d = inventoryDisplayer(inv);
            if (d != null && d.logisticSelector != null)
            {
                logisticSelectorSetListsDisplay.Invoke(d.logisticSelector, new object[0]);
                d.logisticSelector.priority.text = inv.GetLogisticEntity().GetPriority().ToString();
            }

            if (inv.GetLogisticEntity().GetSupplyGroups() == null)
            {
                // Avoid crash in SetInventoryStatusInLogistics if
                // inv is in a task that doesn't have supplygroups initialized yet
                inv.GetLogisticEntity().SetSupplyGroups(new());
            }
            Managers.GetManager<LogisticManager>()?.SetInventoryStatusInLogistics(inv);
        }

        static void SendDroneStats()
        {
            var lm = Managers.GetManager<LogisticManager>();
            if (lm == null)
            {
                return;
            }

            int supplyCount = 0;
            int demandCount = 0;

            foreach (var kv in lm.GetAllCurrentTasks())
            {
                var v = kv.Value;

                if (v.GetTaskState() == LogisticData.TaskState.ToSupply)
                {
                    supplyCount++;
                }
                else if (v.GetTaskState() == LogisticData.TaskState.ToDemand)
                {
                    demandCount++;
                }
            }

            var msg = new MessageDroneStats();
            msg.supplyCount = supplyCount;
            msg.demandCount = demandCount;
            SendAllClients(msg);
        }

        static void ReceiveMessageDroneStats(MessageDroneStats mds)
        {
            droneSupplyCount = mds.supplyCount;
            droneDemandCount = mds.demandCount;
        }

        /// <summary>
        /// The vanilla sets up the station counts and starts a coroutine to update task counts.
        /// 
        /// We have to override this on the host/client because the station counts can change while we are
        /// looking at the window. In addition, we don't sync all tasks so we need to update the supply/demand
        /// counts from a sync message too.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___logisticManager"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowLogistics), nameof(UiWindowLogistics.OnOpen))]
        static void UiWindowLogistics_OnOpen(UiWindowLogistics __instance, LogisticManager ___logisticManager)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                __instance.StopAllCoroutines();
                __instance.StartCoroutine(UiWindowLogistics_UpdateCurrentTaskNumber_Override(__instance, ___logisticManager, 1f));
            }
        }

        static IEnumerator UiWindowLogistics_UpdateCurrentTaskNumber_Override(
            UiWindowLogistics __instance, LogisticManager ___logisticManager, 
            float timeRepeat)
        {
            for (; ; )
            {

                uiWindowLogisticsSetLogisticsList.Invoke(__instance, new object[] { 
                    true, __instance.gridSupply, ___logisticManager.GetSupplyInventories() });
                uiWindowLogisticsSetLogisticsList.Invoke(__instance, new object[] { 
                    false, __instance.gridDemand, ___logisticManager.GetDemandInventories() });

                UiWindowLogistics_updateCurrentTaskNumber(__instance, ___logisticManager);

                yield return new WaitForSeconds(timeRepeat);
            }
        }

        static void UiWindowLogistics_updateCurrentTaskNumber(UiWindowLogistics __instance, LogisticManager ___logisticManager)
        {
            Group groupViaId = GroupsHandler.GetGroupViaId(__instance.dronesGroups[0].id);
            GameObjects.DestroyAllChildren(__instance.gridDrones.gameObject, false);
            int dronesInLogistics = ___logisticManager.GetDronesInLogistics();

            Instantiate(__instance.groupLineGameObject, __instance.gridDrones.transform)
                .GetComponent<UiGroupLine>().SetValues(groupViaId, dronesInLogistics, "");
            
            int supplyCount = droneSupplyCount;
            int demandCount = droneDemandCount;
            if (updateMode == MultiplayerMode.CoopHost)
            {
                Dictionary<int, LogisticTask> allCurrentTasks = ___logisticManager.GetAllCurrentTasks();
                foreach (KeyValuePair<int, LogisticTask> keyValuePair in allCurrentTasks)
                {
                    if (keyValuePair.Value.GetTaskState() == LogisticData.TaskState.ToSupply)
                    {
                        supplyCount++;
                    }
                    else if (keyValuePair.Value.GetTaskState() == LogisticData.TaskState.ToDemand)
                    {
                        demandCount++;
                    }
                }
            }

            Instantiate(__instance.groupLineGameObject, __instance.gridDrones.transform)
                .GetComponent<UiGroupLine>()
                .SetValues(groupViaId, supplyCount, Localization.GetLocalizedString("Logistic_menu_supply"));
            Instantiate(__instance.groupLineGameObject, __instance.gridDrones.transform)
                .GetComponent<UiGroupLine>()
                .SetValues(groupViaId, demandCount, Localization.GetLocalizedString("Logistic_menu_demand"));
        }
    }

    internal class DroneSmoother : MonoBehaviour
    {
        internal Drone drone;
        internal Vector3 targetPosition;
        internal bool targetReached;

        void Update()
        {
            if (drone == null)
            {
                return;
            }

            if (Vector3.Distance(transform.position, targetPosition) >= drone.distanceMinToTarget) 
            {
                transform.Translate(0f, 0f, Time.deltaTime * drone.forwardSpeed);

                var tp = targetPosition + Vector3.up * 2f;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(tp - transform.position),
                    Time.deltaTime * drone.rotationSpeed);
                targetReached = false;
            }
            else
            {
                targetReached = true;
            }
        }
    }
}
