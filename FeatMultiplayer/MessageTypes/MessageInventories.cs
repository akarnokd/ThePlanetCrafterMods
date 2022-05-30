using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageInventories
    {
        internal List<WorldInventory> inventories = new List<WorldInventory> ();

        internal static void Append(StringBuilder sb, Inventory inv, int asId)
        {
            sb.Append(asId);
            sb.Append(';');
            sb.Append(inv.GetSize());
            foreach (WorldObject wo in inv.GetInsideWorldObjects())
            {
                sb.Append(';');
                sb.Append(wo.GetId());
            }
        }

        internal static bool TryParse(string str, out MessageInventories minv)
        {
            if (MessageHelper.TryParseMessage("Inventories|", str, out var parameters))
            {
                try
                {
                    minv = new MessageInventories();
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        string pi = parameters[i];
                        if (pi.Length == 0)
                        {
                            continue;
                        }

                        string[] innerIds = pi.Split(';');

                        WorldInventory wi = new WorldInventory();
                        wi.id = int.Parse(innerIds[0]);
                        wi.size = int.Parse(innerIds[1]);

                        for (int j = 2; j < innerIds.Length; j++)
                        {
                            string ii = innerIds[j];
                            if (ii.Length == 0)
                            {
                                continue;
                            }
                            wi.itemIds.Add(int.Parse(ii));
                        }
                        minv.inventories.Add(wi);
                    }
                    return true;
                } 
                catch (Exception)
                {

                }
            }
            minv = null;
            return false;
        }
    }

    internal class WorldInventory
    {
        internal int id;
        internal int size;
        internal List<int> itemIds = new List<int>();
    }
}
