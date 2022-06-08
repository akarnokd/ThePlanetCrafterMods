using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {

        static void ReceiveMessageGameMode(MessageGameMode mgm)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }

            LogInfo("ReceiveMessageGameMode: Switching to game mode " + mgm.modeIndex);

            var playerStat = new JsonableGameState();
            playerStat.mode = mgm.modeIndex.ToString();

            GameSettingsHandler.SetGameMode(playerStat);

            // we need to reset the consumption tracker values too
            ResetGaugeConsumptions();
        }

        static void ResetGaugeConsumptions()
        {
            var gh = GetPlayerMainController().GetGaugesHandler();

            AccessTools.Field(typeof(PlayerGaugesHandler), "healthChangeValuePerSec").SetValue(gh, GaugesConsumptionHandler.GetHealthConsumptionRate());
            AccessTools.Field(typeof(PlayerGaugesHandler), "thirstChangeValuePerSec").SetValue(gh, GaugesConsumptionHandler.GetThirstConsumptionRate());
            AccessTools.Field(typeof(PlayerGaugesHandler), "outsideOxygenChangeValuePerSec").SetValue(gh, GaugesConsumptionHandler.GetOxygenConsumptionRate());
            AccessTools.Field(typeof(PlayerGaugesHandler), "oxygenChangeValuePerSec").SetValue(gh, GaugesConsumptionHandler.GetOxygenConsumptionRate());
        }

        /// <summary>
        /// The vanilla game calls DyingConsequencesHandler::HandleDyingConsequences when the player
        /// dies to either drop some or all inventories, or outright delete the save file.
        /// 
        /// On the host, we let it happen as usual, but in hardcode, we also stop the network activities.
        /// 
        /// On the client, we send a message to the server to handle the inventory/death chests in
        /// <see cref="ReceiveMessageDeath(MessageDeath)"/>.
        /// </summary>
        /// <param name="_playerMainController">The player's controller to get its death position.</param>
        /// <returns>False if client mode and the death happened in non-chill mode. True otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DyingConsequencesHandler), nameof(DyingConsequencesHandler.HandleDyingConsequences))]
        static bool DyingConsequencesHandler_HandleDyingConsequences(PlayerMainController _playerMainController)
        {
            DataConfig.GameSettingMode gameSettingMode = GameSettingsHandler.GetGameMode();
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (gameSettingMode != DataConfig.GameSettingMode.Chill)
                {
                    Send(new MessageDeath()
                    {
                        position = _playerMainController.transform.position
                    });
                    Signal();

                    if (gameSettingMode == DataConfig.GameSettingMode.Standard)
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("Dying_Info_Drop_Some_Items", 6f, "");
                    }
                    else
                    if (gameSettingMode == DataConfig.GameSettingMode.Intense)
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("Dying_Info_Lost_All_Items", 6f, "");
                    }

                    return false;
                }
            }
            else
            if (updateMode == MultiplayerMode.CoopHost && gameSettingMode == DataConfig.GameSettingMode.Hardcore)
            {
                UiWindowPause_OnQuit();
            }
            return true;
        }

        static void ReceiveMessageDeath(MessageDeath mdt)
        {
            if (updateMode != MultiplayerMode.CoopHost)
            {
                return;
            }

            switch (GameSettingsHandler.GetGameMode())
            {
                case DataConfig.GameSettingMode.Chill:
                    {
                        LogWarning("ReceiveMessageDeath: Chill mode?");
                        break;
                    }
                case DataConfig.GameSettingMode.Standard:
                    {
                        LogInfo("ReceiveMessageDeath: Standard @ " + mdt.position);
                        var inv = InventoriesHandler.GetInventoryById(shadowInventoryId);
                        var list = inv.GetInsideWorldObjects();

                        var dropProbability = 50;

                        if (list.Count != 0)
                        {
                            Inventory dinv = SpawnDeathChest(mdt.position);

                            var dropAll = list.Count <= 4;

                            for (int i = list.Count - 1; i >= 0; i--)
                            {
                                var item = list[i];
                                if (dropAll || dropProbability > UnityEngine.Random.Range(0, 100))
                                {
                                    if (dinv.AddItem(item))
                                    {
                                        inv.RemoveItem(item, false);
                                    }
                                }
                            }

                        }
                        break;
                    }
                case DataConfig.GameSettingMode.Intense:
                    {
                        LogInfo("ReceiveMessageDeath: Intense @ " + mdt.position);
                        DeathClearInventory(shadowInventoryId);
                        
                        break;
                    }
                case DataConfig.GameSettingMode.Hardcore:
                    {
                        LogInfo("ReceiveMessageDeath: Hardcode @ " + mdt.position);
                        DeathClearInventory(shadowInventoryId);
                        DeathClearInventory(shadowEquipmentId);

                        break;
                    }
            }
        }

        static void DeathClearInventory(int iid)
        {
            var inv = InventoriesHandler.GetInventoryById(iid);
            var list = inv.GetInsideWorldObjects();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                inv.RemoveItem(list[i], true);
            }
        }

        static Inventory SpawnDeathChest(Vector3 position)
        {
            var wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container1"), 0);
            wo.SetPositionAndRotation(position, Quaternion.identity);
            wo.SetDontSaveMe(false);

            var go = WorldObjectsHandler.InstantiateWorldObject(wo, false);

            var inv = go.GetComponent<InventoryAssociated>().GetInventory();
            inv.SetSize(30);

            SendWorldObject(wo, false);
            Send(new MessageInventorySize()
            {
                inventoryId = inv.GetId(),
                size = inv.GetSize()
            });
            Signal();
            return inv;
        }
    }
}
