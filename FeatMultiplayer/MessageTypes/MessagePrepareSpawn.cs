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
    /// Sent to the client to add WorldUniqueId and InventoryFromScene components to
    /// the target WorldObject's game object.
    /// </summary>
    internal class MessagePrepareSpawn : MessageBase
    {
        internal int worldObjectId;

        internal static bool TryParse(string str, out MessagePrepareSpawn mis)
        {
            if (MessageHelper.TryParseMessage("PrepareSpawn|", str, 2, out var parameters))
            {
                try
                {
                    mis = new();
                    mis.worldObjectId = int.Parse(parameters[1]);
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
            return "PrepareSpawn|" + worldObjectId + "\n";
        }
    }
}
