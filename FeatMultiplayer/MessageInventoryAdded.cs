using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageInventoryAdded : MessageStringProvider
    {
        internal int inventoryId;
        internal int itemId;
        internal string groupId;

        internal static bool TryParse(string str, out MessageInventoryAdded mia)
        {
            if (MessageHelper.TryParseMessage("InventoryAdd|", str, out var parameters))
            {
                if (parameters.Length == 4)
                {
                    try
                    {
                        mia = new MessageInventoryAdded();
                        mia.inventoryId = int.Parse(parameters[1]);
                        mia.itemId = int.Parse(parameters[2]);
                        mia.groupId = parameters[3];
                        return true;
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            mia = null;
            return false;
        }

        public string GetString()
        {
            return "InventoryAdd|" + inventoryId + "|" + itemId + "|" + groupId + "\n";
        }
    }
}
