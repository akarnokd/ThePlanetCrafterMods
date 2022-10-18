using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageInventorySize : MessageBase
    {
        internal int inventoryId;
        internal int size;

        internal static bool TryParse(string str, out MessageInventorySize mis)
        {
            if (MessageHelper.TryParseMessage("InventorySize|", str, 3, out var parameters))
            {
                try
                {
                    mis = new();
                    mis.inventoryId = int.Parse(parameters[1]);
                    mis.size = int.Parse(parameters[2]);
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
            return "InventorySize|" + inventoryId + "|" + size + "\n";
        }
    }
}
