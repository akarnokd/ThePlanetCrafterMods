// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System;

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
    }
}
