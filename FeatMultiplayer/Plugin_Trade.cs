using BepInEx;
using FeatMultiplayer.MessageTypes;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla invokes this when the player presses the launch button.
        /// 
        /// On the client, we have to ask the host to perform the launch.
        /// 
        /// On the host, we have to notify all clients to ignite their rockets and
        /// track their progress locally too.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___worldObject"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineTradePlatform), nameof(MachineTradePlatform.SendTradeRocket))]
        static bool MachineTradePlatform_SendTradeRocket(MachineTradePlatform __instance, WorldObject ___worldObject)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SendAllClients(new MessageLaunchTrade { platformId = ___worldObject.GetId() }, true);
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                if (!__instance.IsRocketOnSite())
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_nothing_to_launch_in_space", 2f, "");
                }
                else
                {
                    SendHost(new MessageLaunchTrade { platformId = ___worldObject.GetId() }, true);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// This method generates the resources for the trade once the rocket reaches space.
        /// 
        /// We do this on the host and notify the clients about the new objects.
        /// 
        /// Clients do nothing.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___worldObject"></param>
        /// <param name="___inventory"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineTradePlatform), "MakeTrade")]
        static bool MachineTradePlatform_MakeTrade(MachineTradePlatform __instance, 
            WorldObject ___worldObject, Inventory ___inventory)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                return false;
            }
            if (updateMode == MultiplayerMode.CoopHost)
            {
                int num = 0;
                List<WorldObject> insideWorldObjects = ___inventory.GetInsideWorldObjects();
                List<Group> list = new();
                foreach (WorldObject worldObject in insideWorldObjects)
                {
                    num += worldObject.GetGroup().GetTradeValue();
                    list.Add(worldObject.GetGroup());
                }
                ___inventory.RemoveItems(list, true, false);
                TokensHandler.GainTokens(num);
                List<Group> linkedGroups = ___worldObject.GetLinkedGroups();
                if (linkedGroups != null)
                {
                    foreach (Group group in linkedGroups)
                    {
                        if (group.GetTradeValue() > TokensHandler.GetTokensNumber())
                        {
                            break;
                        }
                        WorldObject worldObject2 = WorldObjectsHandler.CreateNewWorldObject(group, 0, null);
                        SendWorldObjectToClients(worldObject2, false);
                        ___inventory.AddItem(worldObject2);
                        TokensHandler.LoseTokens(group.GetTradeValue());
                    }
                    ___worldObject.SetLinkedGroups(null);
                    if (___inventory.GetInsideWorldObjects().Count > 0)
                    {
                        ___worldObject.SetSetting(0);
                    }
                    SendWorldObjectToClients(___worldObject, false);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// In the vanilla, once the inventory gets full and setting == 1, it will
        /// launch the rocket.
        /// 
        /// This will be handled on the Host so do nothing on the client.
        /// </summary>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineTradePlatform), "OnTradeInventoryModified")]
        static bool MachineTradePlatform_OnTradeInventoryModified(MachineTradePlatform __instance)
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        static void ReceiveMessageLaunchTrade(MessageLaunchTrade msg)
        {
            if (worldObjectById.TryGetValue(msg.platformId, out var platformWorldObject))
            {
                if (TryGetGameObject(platformWorldObject, out var platformGameObject) && platformGameObject != null && platformGameObject.activeSelf)
                {
                    var tradePlatform = platformGameObject.GetComponent<MachineTradePlatform>();
                    if (tradePlatform != null)
                    {
                        var rocketGo = tradePlatform.rocket;
                        var machineRocket = rocketGo.GetComponent<MachineRocket>();
                        if (machineRocket == null)
                        {
                            machineRocket = rocketGo.AddComponent<MachineRocket>();
                        }
                        if (updateMode == MultiplayerMode.CoopHost)
                        {
                            SendAllClients(new MessageLaunchTrade() { platformId = platformWorldObject.GetId() }, true);
                        }
                        machineRocket.Ignite(false);
                        PlayerMainController pm = GetPlayerMainController();
                        if (Vector3.Distance(pm.transform.position, platformWorldObject.GetPosition()) < rocketShakeDistance)
                        {
                            pm.GetPlayerCameraShake().SetShaking(true, 0.06f, 0.0035f);
                        }
                        platformWorldObject.SetGrowth(1);

                        CoroutineRunner.StopOn(tradePlatform);
                        CoroutineRunner.StartOn(tradePlatform, (IEnumerator)machineTradePlatformUpdateGrowth.Invoke(tradePlatform, new object[] { tradePlatform.updateGrowthEvery }));

                        // when launching, refresh the dialog
                        CloseTradeWindowIfOpen(tradePlatform);

                        LogInfo("ReceiveMessageLaunchTrade: Launch " + DebugWorldObject(platformWorldObject));
                    }
                    else
                    {
                        LogWarning("ReceiveMessageLaunchTrade: No MachineTradePlatform component = " + DebugWorldObject(platformWorldObject));
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageLaunchTrade: No Trade Platform GameObject = " + DebugWorldObject(platformWorldObject));
                }
            }
            else
            {
                LogWarning("ReceiveMessageLaunchTrade: Unknown platformId " + msg.platformId);
            }
        }

        static void CloseTradeWindowIfOpen(MachineTradePlatform tradePlatform)
        {
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh != null && wh.GetHasUiOpen())
            {
                var wt = wh.GetWindowViaUiId(wh.GetOpenedUi());
                if (wt is UiWindowTrade uiwt && uiWindowTradeMachineTradePlatform(uiwt) == tradePlatform)
                {
                    //LogInfo("Closing the trade window");
                    uiwt.CloseAll();
                    //wh.OpenAndReturnUi(DataConfig.UiType.Trade);
                }
            }
        }

        /// <summary>
        /// The vanilla sets up the token amounts.
        /// 
        /// We need to keep updating this as tokens may be changing by outside means (player, automation).
        /// </summary>
        /// <param name="__instance"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowTrade), nameof(UiWindowTrade.SetTradePlatform))]
        static void UiWindowTrade_SetTradePlatform(UiWindowTrade __instance, 
            MachineTradePlatform ___machineTradePlatform,
            ref Dictionary<Group, int> ___groupsWithNumber)
        {
            if (___groupsWithNumber == null)
            {
                ___groupsWithNumber = new Dictionary<Group, int>();
            }
            __instance.StartCoroutine(UiWindowTrade_UpdateTokens_Loop(__instance, ___machineTradePlatform, ___groupsWithNumber, 0.25f));
        }

        static IEnumerator UiWindowTrade_UpdateTokens_Loop(UiWindowTrade __instance, 
            MachineTradePlatform ___machineTradePlatform,
            Dictionary<Group, int> ___groupsWithNumber,
            float delay)
        {
            for (; ; )
            {
                uiWindowTradeUpdateTokenUi.Invoke(__instance, new object[0]);

                var machineTradeLinkedGroups = ___machineTradePlatform.GetMachineTradeLinkedGroups();

                var counter = ___groupsWithNumber;
                counter.Clear();

                if (machineTradeLinkedGroups != null)
                {
                    foreach (var mg in machineTradeLinkedGroups)
                    {
                        counter.TryGetValue(mg, out var c);
                        counter[mg] = c + 1;
                    }
                }

                var grid = __instance.GetComponentInChildren<GridLayoutGroup>();

                if (grid != null)
                {
                    for (int i = 0; i < grid.transform.childCount; i++)
                    {
                        var go = grid.transform.GetChild(i).gameObject;

                        var line = go.GetComponent<UiGroupLine>();
                        if (line != null)
                        {
                            var gr = uiGroupLineGroup(line);
                            if (gr != null)
                            {
                                counter.TryGetValue(gr, out var n);
                                line.UpdateQuantity(n);
                            }
                        }
                    }
                }

                yield return new WaitForSeconds(delay);
            }
        }

        /// <summary>
        /// Invoked when the user clicks on the resource +/- buttons. It generates a list
        /// of groups to be the linked group. Ordering 100 items of the same group will have
        /// 100 entries in the linked groups!
        /// 
        /// We just notify the other parties about the change.
        /// </summary>
        /// <param name="___worldObject"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineTradePlatform), nameof(MachineTradePlatform.SetMachineTradeLinkedGroups))]
        static void MachineTradePlatform_SetMachineTradeLinkedGroups(WorldObject ___worldObject)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SendWorldObjectToClients(___worldObject, false);
            }
            else if (updateMode == MultiplayerMode.CoopClient)
            {
                SendWorldObjectToHost(___worldObject, false);
            }
        }

        /*
        /// <summary>
        /// For testing purposes, we speed up the rocket progressing.
        /// </summary>
        /// <param name="___updateGrowthEvery"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineTradePlatform), nameof(MachineTradePlatform.SetWorldObjectForTradePlatform))]
        static void MachineTradePlatform_SetWorldObjectForTradePlatform(ref float ___updateGrowthEvery)
        {
            ___updateGrowthEvery = 0.5f;
        }
        */

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryDisplayer), "Consume")]
        static bool InventoryDisplayer_Consume(WorldObject _worldObject, Inventory ___inventory)
        {
            if (_worldObject.GetGroup() is GroupItem gi)
            {
                if (gi.GetUsableType() == DataConfig.UsableType.AquireTokens)
                {
                    if (updateMode == MultiplayerMode.CoopClient)
                    {
                        var msg = new MessageConsume
                        {
                            worldObjectId = _worldObject.GetId(),
                            inventoryId = ___inventory.GetId(),
                        };
                        SendHost(msg);
                        return false;
                    }
                }
            }
            return true;
        }

        static void ReceiveMessageConsume(MessageConsume msg)
        {
            if (updateMode != MultiplayerMode.CoopHost)
            {
                return;
            }
            if (worldObjectById.TryGetValue(msg.worldObjectId, out var wo))
            {
                Inventory inv = null;
                if (msg.inventoryId == 1)
                {
                    inv = msg.sender.shadowBackpack;
                } else
                {
                    inventoryById.TryGetValue(msg.inventoryId, out inv);
                }
                if (inv != null)
                {
                    if (wo.GetGroup() is GroupItem gi)
                    {
                        if (gi.GetUsableType() == DataConfig.UsableType.AquireTokens)
                        {
                            if (inv.ContainWorldObject(wo))
                            {
                                // the fast sync will notify everyone about this change.
                                TokensHandler.GainTokens(gi.GetGroupValue());
                                inv.RemoveItem(wo, true);
                            }
                        }
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageConsume: Unknown inventory " + msg.inventoryId);
                }
            }
            else
            {
                LogWarning("ReceiveMessage: Unknown WorldObject " + msg.worldObjectId);
            }
        }

        /// <summary>
        /// The vanilla now displays the current Terra Token amount in the equipment screen.
        /// 
        /// Since this can change off screen (rocket delivery, other players picking up tokens).
        /// we need to install a periodic updater.
        /// </summary>
        /// <param name="___currentTokens"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowEquipment), nameof(UiWindowEquipment.OnOpen))]
        static void UiWindowEquipment_OnOpen(UiWindowEquipment __instance, TextMeshProUGUI ___currentTokens)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                CoroutineRunner.StopOn(__instance);
                CoroutineRunner.StartOn(__instance, UiWindowEquipment_UpdateTokens(0.25f, ___currentTokens));
            }
        }

        /// <summary>
        /// When the vanilla closes the dialog, we better stop the update coroutine.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="___currentTokens"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowEquipment), nameof(UiWindowEquipment.OnClose))]
        static void UiWindowEquipment_OnClose(UiWindowEquipment __instance, TextMeshProUGUI ___currentTokens)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                CoroutineRunner.StopOn(__instance);
            }
        }

        static IEnumerator UiWindowEquipment_UpdateTokens(float delay, TextMeshProUGUI ___currentTokens)
        {
            for (; ; )
            {
                var wh = Managers.GetManager<WindowsHandler>();
                if (wh == null || ___currentTokens == null)
                {
                    break;
                }

                ___currentTokens.text = TokensHandler.GetTokensNumber().ToString();

                yield return new WaitForSeconds(delay);
            }
        }
    }
}
