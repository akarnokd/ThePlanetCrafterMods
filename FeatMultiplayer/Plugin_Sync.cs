using BepInEx;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static void SendFullState()
        {
            //LogInfo("Begin syncing the entire game state to the client");

            // =========================================================

            SendMessageUnlocks();

            // =========================================================

            SendTerraformState();

            // =========================================================

            SendAllObjects();

            // =========================================================

            UnborkInventories();

            // =========================================================

            SendAllInventories();

            // -----------------------------------------------------

            SignalAllClients();
        }

        static void SendAllInventories()
        {
            HashSet<int> ignoreInventories = new()
            {
                1,
                2
            };
            foreach (var cc in _clientConnections.Values)
            {
                if (cc.shadowBackpack != null)
                {
                    ignoreInventories.Add(cc.shadowBackpack.GetId());
                }
                if (cc.shadowEquipment != null)
                {
                    ignoreInventories.Add(cc.shadowEquipment.GetId());
                }
            }

            foreach (var cc in _clientConnections.Values)
            {
                if (cc.shadowBackpack != null && cc.shadowEquipment != null)
                {
                    var sb = new StringBuilder();
                    sb.Append("Inventories");

                    // Send player equimpent first

                    Inventory inv = cc.shadowEquipment;
                    sb.Append("|");
                    MessageInventories.Append(sb, inv, 2);

                    // Send player inventory next

                    inv = cc.shadowBackpack;
                    sb.Append("|");
                    MessageInventories.Append(sb, inv, 1);

                    // Send all the other inventories after
                    foreach (Inventory inv2 in InventoriesHandler.GetAllInventories())
                    {
                        int id = inv2.GetId();

                        // Ignore Host's own inventory/equipment and any other shadow inventory
                        if (!ignoreInventories.Contains(id))
                        {
                            sb.Append("|");
                            MessageInventories.Append(sb, inv2, id);
                        }
                    }
                    sb.Append('\n');
                    cc.Send(sb.ToString());
                }
            }
        }

        static void SendAllObjects()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AllObjects");
            int count = 0;
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (!wo.GetDontSaveMe() || larvaeGroupIds.Contains(wo.GetGroup().GetId()))
                {
                    int id = wo.GetId();
                    if (id >= shadowInventoryWorldIdStart + 2 * maxShadowInventoryCount)
                    {
                        sb.Append("|");
                        MessageWorldObject.AppendWorldObject(sb, ';', wo, false);
                        //LogInfo("FullSync> " + DebugWorldObject(wo));
                        count++;
                    }
                }
            }
            if (count == 0)
            {
                sb.Append("|");
            }
            sb.Append('\n');
            SendAllClients(sb.ToString());
        }

        static void SendMessageUnlocks()
        {
            MessageUnlocks mu = new MessageUnlocks();
            List<Group> grs = GroupsHandler.GetUnlockedGroups();
            mu.groupIds = new List<string>(grs.Count + 1);
            foreach (Group g in grs)
            {
                mu.groupIds.Add(g.GetId());
            }
            SendAllClients(mu);
        }

        static void SendPeriodicState()
        {
            SendTerraformState();
            SendDroneStats();
            SignalAllClients();
        }

        /// <summary>
        /// Removes duplicate and multi-home world objects from inventories.
        /// </summary>
        static void UnborkInventories()
        {
            Dictionary<int, int> worldObjectToInventoryId = new();
            foreach (Inventory inv in InventoriesHandler.GetAllInventories())
            {
                int currentInvId = inv.GetId();

                List<WorldObject> wos = inv.GetInsideWorldObjects();

                for (int i = wos.Count - 1; i >= 0; i--)
                {
                    WorldObject wo = wos[i];
                    int woid = wo.GetId();

                    if (worldObjectById.ContainsKey(woid))
                    {
                        if (worldObjectToInventoryId.TryGetValue(woid, out var iid))
                        {
                            if (iid != currentInvId)
                            {
                                LogWarning("UnborkInventories: WorldObject " + woid + " (" + wo.GetGroup().GetId() + ")" + " @ " + currentInvId + " also present in " + iid + "! Removing from " + iid);
                            }
                            else
                            {
                                LogWarning("UnborkInventories: WorldObject " + woid + " (" + wo.GetGroup().GetId() + ")" + " @ " + currentInvId + " duplicate found! Removing duplicate.");
                            }
                            wos.RemoveAt(i);
                        }
                        else
                        {
                            worldObjectToInventoryId[woid] = currentInvId;
                        }
                    }
                    else
                    {
                        LogWarning("UnborkInventories: WorldObject " + woid + " (" + wo.GetGroup().GetId() + ")" + " @ " + currentInvId + " no longer exist! Removing from inventory.");
                        wos.RemoveAt(i);
                    }
                }
            }
        }

        static void SendSavedPlayerPosition(ClientConnection cc)
        {
            string playerName = cc.clientName;
            int backpackWoId = cc.shadowBackpackWorldObjectId;

            WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(backpackWoId);

            if (wo != null)
            {
                string[] data = wo.GetText().Split(';');
                if (data.Length >= 2)
                {
                    string[] coords = data[1].Split(',');
                    if (coords.Length == 3)
                    {
                        var pos = new Vector3(
                            float.Parse(coords[0], CultureInfo.InvariantCulture),
                            float.Parse(coords[1], CultureInfo.InvariantCulture),
                            float.Parse(coords[2], CultureInfo.InvariantCulture)
                        );

                        LogInfo("Moving " + playerName + " to its saved position at " + pos);
                        var msg = new MessageMovePlayer()
                        {
                            position = pos
                        };
                        cc.Send(msg);
                        cc.Signal();
                        return;
                    }
                }
                LogInfo("Player " + playerName + " has no saved position info");
                var pm = GetPlayerMainController();
                if (pm != null)
                {
                    var pos2 = pm.transform.position;
                    LogInfo("Moving " + playerName + " to the host at " + pos2);

                    var msg = new MessageMovePlayer()
                    {
                        position = pos2
                    };
                    cc.Send(msg);
                    cc.Signal();
                }
            }
            else
            {
                LogInfo("Warning, no backpack info for " + playerName + " (" + backpackWoId + ")");
            }
        }

        static void StorePlayerPosition(string playerName, int backpackWoId, Vector3 pos)
        {
            WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(backpackWoId);

            if (wo != null)
            {
                var posStr = pos.x.ToString(CultureInfo.InvariantCulture)
                        + "," + pos.y.ToString(CultureInfo.InvariantCulture)
                        + "," + pos.z.ToString(CultureInfo.InvariantCulture);

                string[] data = wo.GetText().Split(';');
                if (data.Length >= 2)
                {
                    data[1] = posStr;
                }
                else
                {
                    var dataNew = new string[2];
                    dataNew[0] = data[0];
                    dataNew[1] = posStr;
                    data = dataNew;
                }

                wo.SetText(string.Join(";", data));
            }
            else
            {
                LogInfo("Warning, no backpack info for " + playerName + " (" + backpackWoId + ")");
            }
        }

        static void SendDroneTargets()
        {
            foreach (var kw in droneTargetCache)
            {
                var msg = new MessageDronePosition();
                msg.id = kw.Key;
                msg.position = kw.Value;
                SendAllClients(msg);
            }
        }

        static void SendGameMode(ClientConnection cc)
        {
            var settings = Managers.GetManager<GameSettingsHandler>().GetCurrentGameSettings();
            cc.Send(new MessageGameMode()
            {
                gameMode = settings.gameMode,
                dyingConsequences = settings.gameDyingConsequences,
                worldSeed = settings.worldSeed,
                unlockedSpaceTrading = settings.unlockedSpaceTrading,
                unlockedOreExtractors = settings.unlockedOreExtrators,
                unlockedDrones = settings.unlockedDrones,
                unlockedAutoCrafter = settings.unlockedAutocrafter,
                unlockedTeleporters = settings.unlockedTeleporters,
                unlockedEverything = settings.unlockedEverything,
                freeCraft = settings.freeCraft,
                randomizeMineables = settings.randomizeMineables,
                terraformationPace = settings.modifierTerraformationPace,
                gaugeDrain = settings.modifierGaugeDrain,
                powerConsumption = settings.modifierPowerConsumption,
                meteoOccurrence = settings.modifierMeteoOccurence
            });
            cc.Signal();
        }
    }
}
