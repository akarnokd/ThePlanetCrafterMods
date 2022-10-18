using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    /// <summary>
    /// Sent by the client to ask the host to generate inventory for a pre-placed chest or
    /// other object with <see cref="SpaceCraft.InventoryFromScene"/>.
    /// </summary>
    internal class MessageInventorySpawn : MessageBase
    {
        internal int inventoryId;

        internal static bool TryParse(string str, out MessageInventorySpawn mis)
        {
            if (MessageHelper.TryParseMessage("InventorySpawn|", str, 2, out var parameters))
            {
                try
                {
                    mis = new();
                    mis.inventoryId = int.Parse(parameters[1]);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mis = null;
            return false;
        }

        public override string GetString()
        {
            return "InventorySpawn|" + inventoryId + "\n";
        }
    }
}
