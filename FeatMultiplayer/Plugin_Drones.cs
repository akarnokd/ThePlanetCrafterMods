using BepInEx;
using FeatMultiplayer.MessageTypes;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
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
        /// We let the clients know of the current position via the world object.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___droneWorldObject"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Drone), "MoveToTarget")]
        static void Drone_MoveToTarget(Drone __instance, WorldObject ___droneWorldObject)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (___droneWorldObject != null)
                {
                    ___droneWorldObject.SetPositionAndRotation(__instance.gameObject.transform.position, __instance.gameObject.transform.rotation);
                    SendWorldObjectToClients(___droneWorldObject, false);
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
                    LogInfo("Drone " + ___droneWorldObject?.GetId() + " hidden");
                }
                ___associatedDroneStation?.OnDroneEnter();
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

        static void ReceiveMessageUpdateSupplyDemand(MessageUpdateSupplyDemand msg)
        {
            Inventory inv = InventoriesHandler.GetInventoryById(msg.inventoryId);

            if (inv != null)
            {
                var le = inv.GetLogisticEntity();

                {
                    var dg = new List<Group>();
                    foreach (var dgm in msg.demandGroups)
                    {
                        var g = GroupsHandler.GetGroupViaId(dgm);
                        if (g != null)
                        {
                            dg.Add(g);
                        }
                        else
                        {
                            LogWarning("ReceiveMessageUpdateSupplyDemand: Inventory " + msg.inventoryId + " unknown demand group " + dgm);
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
                    foreach (var sgm in msg.demandGroups)
                    {
                        var g = GroupsHandler.GetGroupViaId(sgm);
                        if (g != null)
                        {
                            sg.Add(g);
                        }
                        else
                        {
                            LogWarning("ReceiveMessageUpdateSupplyDemand: Inventory " + msg.inventoryId + " unknown supply group " + sgm);
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

                // if the player is looking at it right now.
                var d = inventoryDisplayer(inv);
                if (d != null && d.logisticSelector != null) {
                    logisticSelectorSetListsDisplay.Invoke(d.logisticSelector, new object[0]);
                }
            }
            else
            {
                LogWarning("ReceiveMessageUpdateSupplyDemand: Inventory " + msg.inventoryId + " not found");
            }
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SendAllClientsExcept(msg.sender.id, msg, true);
            }
        }
    }
}
