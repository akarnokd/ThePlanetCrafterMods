using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageInventoryRemoved : MessageInventoryChanged
    {
        internal bool destroy;

        internal static bool TryParse(string str, out MessageInventoryRemoved mir)
        {
            if (MessageHelper.TryParseMessage("InventoryRemoved|", str, out var parameters))
            {
                if (parameters.Length == 4)
                {
                    try
                    {
                        mir = new MessageInventoryRemoved();
                        mir.inventoryId = int.Parse(parameters[1]);
                        mir.itemId = int.Parse(parameters[2]);
                        mir.destroy = "1" == parameters[3];
                        return true;
                    }
                    catch (Exception)
                    {

                    }
                }
                else
                {
                    Plugin.LogError("InventoryRemoved.Length = " + parameters.Length);
                }
            }
            mir = null;
            return false;
        }

        public override string GetString()
        {
            return "InventoryRemoved|" + inventoryId + "|" + itemId + "|" + (destroy ? 1 : 0) + "\n";
        }
    }
}
