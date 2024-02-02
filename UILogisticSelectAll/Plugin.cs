// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace UILogisticSelectAll
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uilogisticselectall", "(UI) Logistic Select All", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GroupSelector), "Start")]
        static void GroupSelector_Start(GroupSelector __instance, List<Group> ___addedGroups)
        {
            var kw = __instance.gameObject.AddComponent<KeyboardWatcher>();
            kw.selector = __instance;
            kw.addedGroups = ___addedGroups;
        }

        static bool suppressSanitizeAndUiUpdates;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticEntity), "SanitizeGroups")]
        static bool LogisticEntity_SanitizeGroups(List<Group> groupsToPrioritize, List<Group> groupsToRemoveFrom)
        {
            if (!suppressSanitizeAndUiUpdates)
            {
                if (groupsToPrioritize != null && groupsToRemoveFrom != null)
                {
                    var set = new HashSet<Group>(groupsToPrioritize);
                    groupsToRemoveFrom.RemoveAll(set.Contains);
                }
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(LogisticSelector), "SetListsDisplay")]
        static bool LogisticSelector_SetListsDisplay()
        {
            return !suppressSanitizeAndUiUpdates;
        }

        internal class KeyboardWatcher : MonoBehaviour
        {
            internal GroupSelector selector;
            internal List<Group> addedGroups;

            public void Update()
            {
                if (selector.listContainer.activeSelf 
                    && Keyboard.current[Key.A].wasPressedThisFrame 
                    && (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed))
                {
                    try
                    {
                        Managers.GetManager<GlobalAudioHandler>().PlayUiSelectElement();
                        for (int i = 0; i < addedGroups.Count; i++)
                        {
                            var g = addedGroups[i];
                            suppressSanitizeAndUiUpdates = i < addedGroups.Count - 1;
                            if (!suppressSanitizeAndUiUpdates)
                            {
                                selector.groupDisplayer.SetGroupAndUpdateDisplay(g, greyed: false, showName: true);
                            }
                            selector.groupSelectedEvent?.Invoke(g);
                        }

                        selector.CloseList();
                    }
                    finally
                    {
                        suppressSanitizeAndUiUpdates = false;
                    }
                }
            }
        }
    }
}
