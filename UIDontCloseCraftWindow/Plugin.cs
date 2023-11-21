using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine.EventSystems;
using BepInEx.Logging;
using System.Reflection;

namespace UIDontCloseCraftWindow
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uidontclosecraftwindow", "(UI) Don't Close Craft Window", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        /// <summary>
        /// Have to pass state between OnImageClicked and TryToCraftInInventory.
        /// </summary>
        static bool rightMouseClicked;
        static bool successfulCraft;
        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowCraft), "OnImageClicked")]
        static bool UiWindowCraft_OnImageClicked_Pre(EventTriggerCallbackData _eventTriggerCallbackData)
        {
            rightMouseClicked = _eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Right;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowCraft), "OnImageClicked")]
        static void UiWindowCraft_OnImageClickedPost(EventTriggerCallbackData _eventTriggerCallbackData)
        {
            if (successfulCraft)
            {
                successfulCraft = false;
                FieldInfo fi = AccessTools.Field(typeof(EventHoverShowGroup), "associatedGroup");
                if (_eventTriggerCallbackData.group != null)
                {
                    foreach (EventHoverShowGroup e in FindObjectsByType<EventHoverShowGroup>(UnityEngine.FindObjectsSortMode.None))
                    {
                        Group g = (Group)fi.GetValue(e);
                        if (g != null && g.GetId() == _eventTriggerCallbackData.group.GetId())
                        {
                            e.OnImageHovered();
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CraftManager), nameof(CraftManager.TryToCraftInInventory))]
        static void CraftManager_TryToCraftInInventory(ref bool __result)
        {
            // In UiWindowCraft.Craft() method, the base.CloseAll() is only invoked if TryToCraftInInventory returned true
            // we pretend it "failed"

            PlayerMainController activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();

            __result = __result && !rightMouseClicked && !activePlayerController.GetPlayerInputDispatcher().IsPressingAccessibilityKey();
            successfulCraft = !__result;
        }
    }
}
