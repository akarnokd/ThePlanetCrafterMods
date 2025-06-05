// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

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

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            moveFew = Config.Bind<int>("General", "MoveFewAmount", 5, "How many items to move when only a few to move.").Value;
            moveMany = Config.Bind<int>("General", "MoveManyAmount", 50, "How many items to move when many to move.").Value;

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), "OnImageClicked")]
        static bool InventoryDisplayer_OnImageClicked(EventTriggerCallbackData eventTriggerCallbackData, Inventory ____inventory)
        {
            if (eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Middle)
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
                    Inventory otherInventory = ((UiWindowContainer)Managers.GetManager<WindowsHandler>().GetWindowViaUiId(openedUi)).GetOtherInventory(____inventory);
                    if (____inventory != null && otherInventory != null)
                    {
                        var gr = eventTriggerCallbackData.worldObject.GetGroup();
                        var id = gr.GetId();

                        List<WorldObject> toTransfer = [];
                        foreach (WorldObject worldObject in ____inventory.GetInsideWorldObjects())
                        {
                            if (worldObject.GetGroup().GetId() == id)
                            {
                                toTransfer.Add(worldObject);
                                if (toTransfer.Count >= max)
                                {
                                    break;
                                }
                            }
                        }
                        var index = 0;
                        var counter = new int[1] { 0 };

                        foreach (WorldObject worldObject2 in toTransfer)
                        {
                            var i = index;
                            InventoriesHandler.Instance.TransferItem(____inventory, otherInventory, worldObject2, success =>
                            {
                                if (success)
                                {
                                    counter[0]++;
                                }
                                if (i == toTransfer.Count - 1)
                                {
                                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("", 3f, "Transfer " + Readable.GetGroupName(gr) + " x " + counter[0]);
                                }
                            });
                            index++;
                        }
                    }
                }
            }
            return true;
        }
    }
}
