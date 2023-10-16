using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using FeatMultiplayer.MessageTypes;

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

            LogInfo("ReceiveMessageGameMode: Switching to game mode " + mgm.gameMode);

            GameSettingsHandler gsh = Managers.GetManager<GameSettingsHandler>();
            var gameSettings = gsh.GetCurrentGameSettings();

            gameSettings.gameMode = mgm.gameMode;
            gameSettings.gameDyingConsequences = mgm.dyingConsequences;
            gameSettings.unlockedSpaceTrading = mgm.unlockedSpaceTrading;
            gameSettings.unlockedOreExtrators = mgm.unlockedOreExtractors;
            gameSettings.worldSeed = mgm.worldSeed;


            gameSettings.unlockedTeleporters = mgm.unlockedTeleporters;

            gameSettings.unlockedDrones = mgm.unlockedDrones;

            gameSettings.unlockedAutocrafter = mgm.unlockedAutoCrafter;

            gameSettings.unlockedEverything = mgm.unlockedEverything;

            gameSettings.freeCraft = mgm.freeCraft;

            gameSettings.randomizeMineables = mgm.randomizeMineables;

            gameSettings.modifierTerraformationPace = mgm.terraformationPace;

            gameSettings.modifierPowerConsumption = mgm.powerConsumption;

            gameSettings.modifierGaugeDrain = mgm.gaugeDrain;

            gameSettings.modifierMeteoOccurence = mgm.meteoOccurrence;

            // we need to reset the consumption tracker values too
            ResetGaugeConsumptions();

            gsh.ChangeGenerationForGroups();
            gsh.AddUnlockedGroups();

            WorldRandomizer worldRandomizer = Managers.GetManager<WorldRandomizer>();
            worldRandomizer.Init();

            if (mgm.randomizeMineables)
            {
                foreach (var wos in FindObjectsByType<WorldObjectFromScene>(FindObjectsSortMode.None))
                {
                    worldRandomizer.ReplaceWorldObjectFromScene(wos);
                }

                foreach (var ov in FindObjectsByType<MachineGenerationGroupVein>(FindObjectsSortMode.None))
                {
                    worldRandomizer.ReplaceOreVein(ov);
                }
            }
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
            var dyingConsequnce = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetDyingConsequences();
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (dyingConsequnce != DataConfig.GameSettingDyingConsequences.NoConsequences)
                {
                    SendHost(new MessageDeath()
                    {
                        position = _playerMainController.transform.position
                    }, true);

                    if (dyingConsequnce == DataConfig.GameSettingDyingConsequences.DropSomeItems)
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("Dying_Info_Drop_Some_Items", 6f, "");
                    }
                    else
                    if (dyingConsequnce == DataConfig.GameSettingDyingConsequences.DropAllItems)
                    {
                        Managers.GetManager<BaseHudHandler>().DisplayCursorText("Dying_Info_Lost_All_Items", 6f, "");
                    }

                    return false;
                }
            }
            else
            if (updateMode == MultiplayerMode.CoopHost && dyingConsequnce == DataConfig.GameSettingDyingConsequences.DeleteSaveFile)
            {
                UiWindowPause_OnQuit();
            }
            return true;
        }

        static void ReceiveMessageDeath(MessageDeath mdt)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (worldObjectById.TryGetValue(mdt.chestId, out var wo))
                {
                    if (TryGetGameObject(wo, out var go))
                    {
                        InventoryAssociated ia= go.GetComponent<InventoryAssociated>();
                        go.AddComponent<InventoryDestroyWhenEmpty>()
                            .SetReferenceInventory(ia.GetInventory());
                    }
                    else
                    {
                        LogWarning("Unknown GameObject for " + DebugWorldObject(wo));
                    }
                }
                else
                {
                    LogWarning("Unknonw WorldObject " + mdt.chestId + " @ " + mdt.position);
                }
            }
            else
            {

                switch (Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings().GetGameMode())
                {
                    case DataConfig.GameSettingMode.Chill:
                        {
                            LogWarning("ReceiveMessageDeath: Chill mode?");
                            break;
                        }
                    case DataConfig.GameSettingMode.Standard:
                        {
                            LogInfo("ReceiveMessageDeath: Standard @ " + mdt.position);
                            var inv = mdt.sender.shadowBackpack;
                            var list = inv.GetInsideWorldObjects();

                            var dropProbability = 50;

                            if (list.Count != 0)
                            {
                                Inventory dinv = SpawnDeathChest(mdt.position, out var chestId);

                                var dropAll = list.Count <= 4;

                                for (int i = list.Count - 1; i >= 0; i--)
                                {
                                    var item = list[i];
                                    if (dropAll || dropProbability > UnityEngine.Random.Range(0, 100))
                                    {
                                        var gr = item.GetGroup();
                                        if (!(gr is GroupItem) || !((GroupItem)gr).GetCantBeDestroyed())
                                        {
                                            if (dinv.AddItem(item))
                                            {
                                                inv.RemoveItem(item, false);
                                            }
                                        }
                                    }
                                }

                                // make the chest disappear upon emptying it
                                mdt.chestId = chestId;
                                mdt.sender.Send(mdt);
                                mdt.sender.Signal();
                            }
                            break;
                        }
                    case DataConfig.GameSettingMode.Intense:
                        {
                            LogInfo("ReceiveMessageDeath: Intense @ " + mdt.position);
                            DeathClearInventoryExcept(mdt.sender.shadowBackpack);

                            break;
                        }
                    case DataConfig.GameSettingMode.Hardcore:
                        {
                            LogInfo("ReceiveMessageDeath: Hardcode @ " + mdt.position);
                            DeathClearInventoryExcept(mdt.sender.shadowBackpack);
                            DeathClearInventory(mdt.sender.shadowEquipment);

                            break;
                        }
                }
            }
        }

        static void DeathClearInventory(Inventory inv)
        {
            var list = inv.GetInsideWorldObjects();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                inv.RemoveItem(list[i], true);
            }
        }

        static void DeathClearInventoryExcept(Inventory inv)
        {
            var list = inv.GetInsideWorldObjects();
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var item = list[i];
                var gr = item.GetGroup();
                if (!(gr is GroupItem) || !((GroupItem)gr).GetCantBeDestroyed())
                {
                    inv.RemoveItem(list[i], true);
                }
            }
        }

        /// <summary>
        /// Tracks the given world object and chest inventory,
        /// destroys them if the chest becomes empty on the host.
        /// </summary>
        class DestroyDeathChest : MonoBehaviour
        {
            internal WorldObject worldObject;
            internal Inventory inventory;

            internal void BeginTrack()
            {
                inventory.inventoryContentModified = (InventoryModifiedEvent)Delegate.Combine(
                    inventory.inventoryContentModified, 
                    new InventoryModifiedEvent(this.OnInventoryModified));
            }

            void OnInventoryModified(WorldObject _worldObjectModified, bool _added)
            {
                if (this.inventory.GetInsideWorldObjects().Count <= 0)
                {
                    WorldObjectsHandler.DestroyWorldObject(worldObject);
                    TryRemoveGameObject(worldObject);
                    UnityEngine.Object.Destroy(gameObject);
                }
            }
        }

        static Inventory SpawnDeathChest(Vector3 position, out int chestId)
        {
            var wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container1"), 0);
            wo.SetPositionAndRotation(position, Quaternion.identity);
            wo.SetDontSaveMe(false);

            var go = WorldObjectsHandler.InstantiateWorldObject(wo, false);

            var inv = go.GetComponent<InventoryAssociated>().GetInventory();
            inv.SetSize(30);
            
            Destroy(go.GetComponentInChildren<ActionDeconstructible>());
            var ddc = go.AddComponent<DestroyDeathChest>();
            ddc.worldObject = wo;
            ddc.inventory = inv;
            ddc.BeginTrack();

            SendWorldObjectToClients(wo, false);
            SendAllClients(new MessageInventorySize()
            {
                inventoryId = inv.GetId(),
                size = inv.GetSize()
            }, true);
            chestId = wo.GetId();
            return inv;
        }
    }
}
