using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageInventorySpawn : MessageStringProvider
    {
        internal int inventoryId;
        internal string sceneName;

        internal static bool TryParse(string str, out MessageInventorySpawn mis)
        {
            if (MessageHelper.TryParseMessage("InventorySpawn|", str, 3, out var parameters))
            {
                try
                {
                    mis = new();
                    mis.inventoryId = int.Parse(parameters[1]);
                    mis.sceneName = parameters[2];
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

        public string GetString()
        {
            return "InventorySpawn|" + inventoryId + "|" + sceneName + "\n";
        }
    }
}
