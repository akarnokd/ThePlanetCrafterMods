using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace UIDontCloseCraftWindow
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uidontclosecraftwindow", "(UI) Don't Close Craft Window", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

		/// <summary>
		/// Have to pass state between OnImageClicked and TryToCraftInInventory.
		/// </summary>
		private static bool rightMouseClicked;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowCraft), "OnImageClicked")]
        static bool UiWindowCraft_OnImageClicked(EventTriggerCallbackData eventTriggerCallbackData)
        {
            rightMouseClicked = eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Right;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static void CraftManager_TryToCraftInInventory(ref bool __result)
        {
            // In UiWindowCraft.Craft() method, the base.CloseAll() is only invoked if TryToCraftInInventory returned true
            // we pretend it "failed"
            __result = __result && !rightMouseClicked;
        }
    }
}
