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
        static bool LogisticManager_SetLogisticTasks_Pre(
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

            Drone drone
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
    }
}
