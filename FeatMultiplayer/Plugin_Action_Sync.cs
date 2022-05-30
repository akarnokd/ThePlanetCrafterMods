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

            MessageTerraformState mts = new MessageTerraformState();

            WorldUnitsHandler wuh = Managers.GetManager<WorldUnitsHandler>();
            mts.oxygen = wuh.GetUnit(DataConfig.WorldUnitType.Oxygen).GetValue();
            mts.heat = wuh.GetUnit(DataConfig.WorldUnitType.Heat).GetValue();
            mts.pressure = wuh.GetUnit(DataConfig.WorldUnitType.Pressure).GetValue();
            mts.biomass = wuh.GetUnit(DataConfig.WorldUnitType.Biomass).GetValue();

            _sendQueue.Enqueue(mts);

            // =========================================================

            StringBuilder sb = new StringBuilder();
            sb.Append("AllObjects");
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (!wo.GetDontSaveMe())
                {
                    int id = wo.GetId();
                    if (id != shadowInventoryWorldId && id != shadowEquipmentWorldId)
                    {
                        sb.Append("|");
                        MessageAllObjects.AppendWorldObject(sb, ';', wo, false);
                        //LogInfo("FullSync> " + DebugWorldObject(wo));
                    }
                }
            }
            sb.Append('\n');
            _sendQueue.Enqueue(sb.ToString());

            // =========================================================

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
    }
}
