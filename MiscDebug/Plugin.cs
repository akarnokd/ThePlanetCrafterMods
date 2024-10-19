// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

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

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WindowsHandler), nameof(WindowsHandler.OpenAndReturnUi))]
        static void WindowsHandler_OpenAndReturnUi(DataConfig.UiType uiId)
        {
            logger.LogInfo("---> " + uiId);
            logger.LogInfo(Environment.StackTrace);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGroupSelector), nameof(ActionGroupSelector.OnAction))]
        static void ActionGroupSelector_OnAction()
        {
            logger.LogInfo(Environment.StackTrace);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionGroupSelector), "OpenInventories")]
        static void ActionGroupSelector_OpenInventories()
        {
            logger.LogInfo(Environment.StackTrace);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), "OnOpen")]
        static void UiWindowGroupSelector_OnOpen()
        {
            logger.LogInfo(Environment.StackTrace);
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowGroupSelector), "OnOpenAutoCrafter")]
        static void UiWindowGroupSelector_OnOpenAutoCrafter()
        {
            logger.LogInfo(Environment.StackTrace);
        }
        */

        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LogisticTask), MethodType.Constructor, 
            [typeof(WorldObject), typeof(Inventory), typeof(Inventory), typeof(WorldObject), typeof(WorldObject), typeof(bool)])
        ]
        static void LogisticTask_Constuctor(Inventory _demandInventory, WorldObject _worldObjectToMove)
        {

        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticTask), nameof(LogisticTask.SetTaskState))]
        static void LogisticTask_SetTaskState(Inventory __demandInventoryWorldObject, WorldObject ___worldObjectToMove)
        {

        }
        */
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), nameof(Drone.SetLogisticTask))]
        static void Drone_SetLogisticTask(LogisticTask ____logisticTask, LogisticTask droneTask)
        {
            if (____logisticTask != null && droneTask != ____logisticTask 
                && ____logisticTask.GetTaskState() != LogisticData.TaskState.Done)
            {
                logger.LogError(
                    "IllegalStateException: Old non-done logistic task found"
                    + "\nat  WO: " + ____logisticTask.GetWorldObjectToMove().GetId() + " (" + ____logisticTask.GetWorldObjectToMove().GetGroup().GetId() + ")"
                    + "\nat  To: " + ____logisticTask.GetDemandInventory().GetId() + " of " + ____logisticTask.GetDemandInventoryWorldObject().GetId()
                    + "\nat  St: " + ____logisticTask.GetTaskState()
                    + "\nat\n"
                    + Environment.StackTrace
                );
            }
        }
        */
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Drone), "OnDestroy")]
        static void Drone_OnDestroy(LogisticTask ____logisticTask)
        {
            if (____logisticTask != null
                && ____logisticTask.GetTaskState() != LogisticData.TaskState.Done)
            {
                logger.LogError(
                    "IllegalStateException: Old non-done logistic task found"
                    + "\n  at  WO: " + ____logisticTask.GetWorldObjectToMove().GetId() + " (" + ____logisticTask.GetWorldObjectToMove().GetGroup().GetId() + ")"
                    + "\n  at  To: " + ____logisticTask.GetDemandInventory().GetId() + " of " + ____logisticTask.GetDemandInventoryWorldObject().GetId()
                    + "\n  at  St: " + ____logisticTask.GetTaskState()
                    + "\n  at\n"
                    + Environment.StackTrace
                );
            }
        }
        */

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), "RetrieveInventoryClientRpc")]
        static void InventoriesHandler_RetrieveInventoryClientRpc(
            Queue<Action<Inventory>> ____callbackQueue,
            InventoriesHandler __instance,
            int inventoryId,
            ref ClientRpcParams clientRpcParams
        )
        {
            var stg = (int)AccessTools.Field(typeof(InventoriesHandler), "__rpc_exec_stage").GetValue(__instance);

            if (!(stg != 1 || (!__instance.NetworkManager.IsClient && !__instance.NetworkManager.IsHost)) && ____callbackQueue.Count == 0)
            {
                ____callbackQueue.Enqueue(null);
            }
        }
        */
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), "RetrieveInventoryClientRpc")]
        static bool InventoriesHandler_RetrieveInventoryClientRpc(
            InventoriesHandler __instance,
            ref ClientRpcParams clientRpcParams,
            Queue<Action<Inventory>> ____callbackQueue
        )
        {
            NetworkManager networkManager = __instance.NetworkManager;
            if (networkManager != null && networkManager.IsListening && networkManager.IsHost)
            {
                if (clientRpcParams.Send.TargetClientIds?.ContainsByEquals(networkManager.LocalClientId) ?? false)
                {
                    return false;
                }
                var na = clientRpcParams.Send.TargetClientIdsNativeArray;
                if (na != null && na.Value.ContainsByEquals(networkManager.LocalClientId))
                {
                    return false;
                }
            }
            return true;
        }
        */

        /*
        static string clientIdStr(NativeArray<ulong>? param)
        {
            if (param == null)
            {
                return "";
            }
            return string.Join(",", param.Value);
        }
        */

        /*
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoriesHandler), "RetrieveInventoryClientRpc")]
        static void InventoriesHandler_RetrieveInventoryClientRpc(
            Queue<Action<Inventory>> ____callbackQueue, 
            InventoriesHandler __instance,
            int inventoryId,
            Exception __exception,
            ref ClientRpcParams clientRpcParams
        )
        {
            if (____callbackQueue.Count == 0 && __exception != null)
            {
                var stg = AccessTools.Field(typeof(InventoriesHandler), "__rpc_exec_stage").GetValue(__instance);

                logger.LogError("IllegalState: Queue is empty for RetrieveInventoryClientRpc: " + inventoryId + " on " 
                    + ("- IsSpawned: " + __instance.IsSpawned)
                    + ("- IsServer: " + __instance.NetworkManager.IsServer)
                    + ("- IsHost: " + __instance.NetworkManager.IsHost)
                    + ("- IsIsClient: " + __instance.NetworkManager.IsClient)
                    + "- Stg: " + stg
                    + " - ClientIds " + clientIdStr(clientRpcParams.Send.TargetClientIdsNativeArray)
                    + "\n" + Environment.StackTrace
                    );
            }
        }
        */
        /*
        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoriesHandler), "RetrieveInventoryClientRpc")]
        static Exception InventoriesHandler_RetrieveInventoryClientRpc(
            Exception __exception
        )
        {
            if (__exception is InvalidOperationException && __exception.Message.Contains("Queue empty"))
            {
                logger.LogInfo("InventoriesHandler::RetrieveInventoryClientRpc crashed with Queue empty. Suppressed.");
                return null;
            }
            return __exception;
        }
        */
        /*

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkUtils), nameof(NetworkUtils.GetSenderClientParams), [typeof(ServerRpcParams)])]
        static void NetworkUtils_GetSenderClientParams(ref ServerRpcParams serverRpcParams)
        {
            logger.LogInfo("NetworkUtils::GetSenderClientParams " + serverRpcParams.Receive.SenderClientId);
        }
        */
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), "UpdateOrCreateInventoryFromMessage")]
        static void InventoriesHandler_UpdateOrCreateInventoryFromMessage_Pre(ref Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoriesHandler), "UpdateOrCreateInventoryFromMessage")]
        static void InventoriesHandler_UpdateOrCreateInventoryFromMessage_Post(ref Stopwatch __state)
        {
            if (!(NetworkManager.Singleton?.IsServer ?? true))
            {
                logger.LogInfo(string.Format("UpdateOrCreateInventoryFromMessage: {0:0.000} ms", __state.Elapsed.TotalMilliseconds));
            }
        }
        */

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrower), nameof(MachineGrower.SetGrowerInventory))]
        static void MachineGrower_SetGrowerInventory(MachineGrower __instance)
        {
            __instance.gameObject.AddComponent<MachineGrowerTracker>();
        }

        public class MachineGrowerTracker : MonoBehaviour
        {
            public void OnDisable()
            {
                // If object will destroy in the end of current frame..
                if (gameObject.activeInHierarchy)
                {
                    logger.LogInfo("Grower Destroyed:\n" + Environment.StackTrace);
                }
                // If object just deactivated..
                else
                {

                }
            }
        }
        */
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlanetList), nameof(PlanetList.GetIsPlanetPurchased))]
        static bool PlanetList_GetIsPlanetPurchased(ref bool __result)
        {
            __result = true;
            return false;
        }
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldInstanceDataSerializationExtensions), nameof(WorldInstanceDataSerializationExtensions.WriteValueSafe))]
        static void WorldInstanceDataSerializationExtensions_WriteValueSafe(WorldInstanceData data)
        {
            foreach (Group group in data.GetRecipe().GetIngredientsGroupInRecipe())
            {
                if (GroupsHandler.GetGroupFromHash(group.stableHashCode) == null)
                {
                    string str = "Group " + group.id + " hash " + group.stableHashCode + " not in GroupsHandler in scene " + data.GetSceneName() + "\r\n";
                    File.AppendAllLines(Application.persistentDataPath + "/host.log", [str]);
                }
            }
        }
        */
        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldInstanceDataSerializationExtensions), nameof(WorldInstanceDataSerializationExtensions.ReadValueSafe))]
        static void WorldInstanceDataSerializationExtensions_ReadValueSafe(ref WorldInstanceData data)
        {
            logger.LogInfo("GroupsHandler.GetAllGroups.Count = " + (GroupsHandler.GetAllGroups(false)?.Count ?? -1));
        }
        */

        /*
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineDisintegrator), "Start")]
        static void MachineDisintegrator_Start(ref int ___breakEveryXSec) 
        {
            ___breakEveryXSec = 1;
        }
        */
    }
}
