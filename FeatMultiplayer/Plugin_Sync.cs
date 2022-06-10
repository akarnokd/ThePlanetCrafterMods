using BepInEx;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                if (!wo.GetDontSaveMe())
                {
                    int id = wo.GetId();
                    if (id != shadowInventoryWorldId && id != shadowEquipmentWorldId)
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

            Inventory inv = InventoriesHandler.GetInventoryById(shadowEquipmentId);
            sb.Append("|");
            MessageInventories.Append(sb, inv, 2);

            // Send player inventory next

            inv = InventoriesHandler.GetInventoryById(shadowInventoryId);
            sb.Append("|");
            MessageInventories.Append(sb, inv, 1);

            // Send all the other inventories after
            foreach (Inventory inv2 in InventoriesHandler.GetAllInventories())
            {
                int id = inv2.GetId();

                // Ignore Host's own inventory/equipment
                if (id != 1 && id != 2 && id != shadowInventoryId && id != shadowEquipmentId)
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

                    if (worldObjectToInventoryId.TryGetValue(woid, out var iid))
                    {
                        if (iid != currentInvId)
                        {
                            LogWarning("UnborkInventories: WorldObject " + woid + " @ " + currentInvId + " also present in " + iid + "! Removing from " + iid);
                        }
                        else
                        {
                            LogWarning("UnborkInventories: WorldObject " + woid + " @ " + currentInvId + " duplicate found! Removing duplicate.");
                        }
                        wos.RemoveAt(i);
                    }
                    else
                    {
                        worldObjectToInventoryId[woid] = currentInvId;
                    }
                }
            }
        }
    }
}
