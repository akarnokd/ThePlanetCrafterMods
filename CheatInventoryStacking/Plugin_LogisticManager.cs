using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;

namespace CheatInventoryStacking
{
    public partial class Plugin
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "SetLogisticTasks")]
        static bool LogisticManager_SetLogisticTasks(
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

            TODO

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticManager), "FindDemandForDroneOrDestroyContent")]
        static bool LogisticManager_FindDemandForDroneOrDestroyContent(
            Drone drone,
            List<Inventory> ____demandInventories
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
                if (inv.GetLogisticEntity().GetDemandGroups().Contains(gr) 
                    && !IsFullStackedOfInventory(inv, gid))
                {
                    var task = CreateNewTaskForWorldObject(droneInv, inv, wo);
                    if (task != null)
                    {
                        task.SetTaskState(LogisticData.TaskState.ToDemand);
                        drone.SetLogisticTask(task);
                        return false;
                    }
                }
            }

            WorldObjectsHandler.Instance.DestroyWorldObject(wo);

            return false;
        }

        static LogisticTask CreateNewTaskForWorldObject(
            Inventory source, Inventory target, WorldObject worldObject)
        {
            TODO
            throw new NotImplementedException();
        }
    }
}
