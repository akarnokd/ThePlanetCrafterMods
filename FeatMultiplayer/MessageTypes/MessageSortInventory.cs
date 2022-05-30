using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageSortInventory : MessageStringProvider
    {
        internal int inventoryId;

        internal static bool TryParse(string str, out MessageSortInventory msi)
        {
            if (MessageHelper.TryParseMessage("SortInventory|", str, 2, out var parameters))
            {
                try
                {
                    msi = new MessageSortInventory();
                    msi.inventoryId = int.Parse(parameters[1]);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            msi = null;
            return false;
        }

        public string GetString()
        {
            return "SortInventory|" + inventoryId + "\n";
        }
    }
}
