using BepInEx;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static void SendFullState()
        {
            //LogInfo("Begin syncing the entire game state to the client");

            // =========================================================

            MessageUnlocks mu = new MessageUnlocks();
            List<Group> grs = GroupsHandler.GetUnlockedGroups();
            mu.groupIds = new List<string>(grs.Count + 1);
            foreach (Group g in grs)
            {
                mu.groupIds.Add(g.GetId());
            }
            _sendQueue.Enqueue(mu);

            // =========================================================

            SendTerraformState();

            // =========================================================

            StringBuilder sb = new StringBuilder();
            sb.Append("AllObjects");
            int count = 0;
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (!wo.GetDontSaveMe() || larvaeGroupIds.Contains(wo.GetGroup().GetId()))
                {
                    int id = wo.GetId();
                    if (id != shadowBackpackWorldObjectId && id != shadowEquipmentWorldObjectId)
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
            _sendQueue.Enqueue(sb.ToString());

            // =========================================================

            UnborkInventories();

            sb = new StringBuilder();
            sb.Append("Inventories");

            // Send player equimpent first

            Inventory inv = shadowEquipment;
            sb.Append("|");
            MessageInventories.Append(sb, inv, 2);

            // Send player inventory next

            inv = shadowBackpack;
            sb.Append("|");
            MessageInventories.Append(sb, inv, 1);

            // Send all the other inventories after
            foreach (Inventory inv2 in InventoriesHandler.GetAllInventories())
            {
                int id = inv2.GetId();

                // Ignore Host's own inventory/equipment
                if (id != 1 && id != 2 && id != shadowBackpack.GetId() && id != shadowEquipment.GetId())
                {
                    sb.Append("|");
                    MessageInventories.Append(sb, inv2, id);
                }
            }
            sb.Append('\n');
            _sendQueue.Enqueue(sb.ToString());

            // -----------------------------------------------------

            Signal();
        }

        static void SendPeriodicState()
        {
            SendTerraformState();
            Signal();
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

        static void SendSavedPlayerPosition(string playerName, int backpackWoId)
        {
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
                        Send(msg);
                        Signal();
                        return;
                    }
                }
                LogInfo("Player " + playerName + " has no saved position info");
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
    }
}
