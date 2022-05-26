using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;

namespace FixUnofficialPatches
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunofficialpatches", "(Fix) Unofficial Patches", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        /// <summary>
        /// Fixes the lack of localization Id when viewing a Craft Station T2, so the window title is not properly updated.
        /// </summary>
        /// <param name="__instance">The ActionCrafter instance of the station object</param>
        /// <param name="___titleLocalizationId">The field hosting the localization id</param>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionCrafter), nameof(ActionCrafter.OnAction))]
        static void ActionCrafter_OnAction(ActionCrafter __instance, ref string ___titleLocalizationId)
        {
            if (__instance.GetCrafterIdentifier() == DataConfig.CraftableIn.CraftStationT2)
            {
                ___titleLocalizationId = "GROUP_NAME_CraftStation1";
            }
        }

        void Update()
        {
            SaveFilesSelectorScrollFix();
        }

        static SaveFilesSelector saveFilesSelectorInstance;
        static FieldInfo saveFilesSelectorObjectsInList;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveFilesSelector), nameof(SaveFilesSelector.Start))]
        static void SaveFilesSelector_Start(SaveFilesSelector __instance)
        {
            saveFilesSelectorInstance = __instance;
            saveFilesSelectorObjectsInList = AccessTools.Field(typeof(SaveFilesSelector), "objectsInList");
        }

        static void SaveFilesSelectorScrollFix()
        {
            if (saveFilesSelectorInstance != null && saveFilesSelectorInstance.filesListContainer.transform.parent.gameObject.activeSelf)
            {
                var scrollBox = saveFilesSelectorInstance.GetComponentInChildren<ScrollRect>();
                float contentHeight = scrollBox.content.sizeDelta.y;

                var scroll = Mouse.current.scroll.ReadValue();
                if (scroll.y != 0)
                {
                    var counts = (List<GameObject>)saveFilesSelectorObjectsInList.GetValue(saveFilesSelectorInstance);
                    if (counts.Count != 0)
                    {
                        if (scroll.y < 0)
                        {
                            scrollBox.verticalNormalizedPosition -= 1f / counts.Count;
                        }
                        else if (scroll.y > 0)
                        {
                            scrollBox.verticalNormalizedPosition += 1f / counts.Count;
                        }
                    }
                }
            }
        }
    }
}
