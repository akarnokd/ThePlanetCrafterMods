using BepInEx;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace UIInventoryMoveMultiple
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiinventorymovemultiple", "(UI) Inventory Move Multiple Items", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        /// <summary>
        /// How many items to move when only a few to move
        /// </summary>
        static int moveFew;
        /// <summary>
        /// How many items to move when many to move.
        /// </summary>
        static int moveMany;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            moveFew = Config.Bind<int>("General", "MoveFewAmount", 5, "How many items to move when only a few to move.").Value;
            moveMany = Config.Bind<int>("General", "MoveManyAmount", 50, "How many items to move when many to move.").Value;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), "OnImageClicked")]
        static bool InventoryDisplayer_OnImageClicked(EventTriggerCallbackData _eventTriggerCallbackData, Inventory ___inventory)
        {
            if (_eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Middle)
            {
                int max = int.MaxValue;
                if (Keyboard.current[Key.LeftShift].isPressed)
                {
                    if (Keyboard.current[Key.LeftCtrl].isPressed)
                    {
                        max = moveMany;
                    }
                    else
                    {
                        max = moveFew;
                    }
                }
                DataConfig.UiType openedUi = Managers.GetManager<WindowsHandler>().GetOpenedUi();
                if (openedUi == DataConfig.UiType.Container || openedUi == DataConfig.UiType.GroupSelector)
                {
                    Inventory otherInventory = ((UiWindowContainer)Managers.GetManager<WindowsHandler>().GetWindowViaUiId(openedUi)).GetOtherInventory(___inventory);
                    if (___inventory != null && otherInventory != null)
                    {
                        string id = _eventTriggerCallbackData.worldObject.GetGroup().GetId();
                        List<WorldObject> list = new List<WorldObject>();
                        foreach (WorldObject worldObject in ___inventory.GetInsideWorldObjects())
                        {
                            if (worldObject.GetGroup().GetId() == id)
                            {
                                list.Add(worldObject);
                            }
                        }
                        int c = 0;
                        foreach (WorldObject worldObject2 in list)
                        {
                            if (otherInventory.AddItem(worldObject2))
                            {
                                ___inventory.RemoveItem(worldObject2, false);
                                if (++c >= max)
                                {
                                    break;
                                }
                            }
                        }
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Transfer " + id + " x " + c);
                    }
                }
            }
            return true;
        }
    }
}
