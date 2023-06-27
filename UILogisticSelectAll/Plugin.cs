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

        private void Awake()
        {
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
        static bool LogisticEntity_SanitizeGroups(List<Group> _groupsToPrioritize, List<Group> _groupsToRemoveFrom)
        {
            if (!suppressSanitizeAndUiUpdates)
            {
                if (_groupsToPrioritize != null && _groupsToRemoveFrom != null)
                {
                    var set = new HashSet<Group>(_groupsToPrioritize);
                    _groupsToRemoveFrom.RemoveAll(set.Contains);
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

            void Update()
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
                            Group g = addedGroups[i];
                            suppressSanitizeAndUiUpdates = i < addedGroups.Count - 1;
                            selector.groupSelectedEvent?.Invoke(g);
                        }

                        selector.listContainer.SetActive(false);
                        Managers.GetManager<DisplayersHandler>().GetGroupInfosDisplayer().Hide();
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
