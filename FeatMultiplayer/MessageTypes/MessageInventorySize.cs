using System;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageInventorySize : MessageBase
    {
        internal int inventoryId;
        internal int size;
        internal bool relative;

        internal static bool TryParse(string str, out MessageInventorySize mis)
        {
            if (MessageHelper.TryParseMessage("InventorySize|", str, 4, out var parameters))
            {
                try
                {
                    mis = new();
                    mis.inventoryId = int.Parse(parameters[1]);
                    mis.size = int.Parse(parameters[2]);
                    mis.relative = "1" == parameters[3];
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
            return "InventorySize|" + inventoryId + "|" + size + "|" + (relative ? "1" : "0") + "\n";
        }
    }
}
