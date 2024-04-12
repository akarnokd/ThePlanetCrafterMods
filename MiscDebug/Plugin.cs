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
            //Harmony.CreateAndPatchAll(typeof(Plugin));
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
    }
}
