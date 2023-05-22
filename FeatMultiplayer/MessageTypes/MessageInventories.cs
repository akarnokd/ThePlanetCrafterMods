using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageInventories : MessageBase
    {
        internal List<WorldInventory> inventories = new List<WorldInventory> ();

        internal static void Append(StringBuilder sb, Inventory inv, int asId)
        {
            sb.Append(asId);
            sb.Append(';');
            sb.Append(inv.GetSize());
            sb.Append(';');
            int i = 0;
            foreach (WorldObject wo in inv.GetInsideWorldObjects())
            {
                if (i != 0)
                {
                    sb.Append(',');
                }
                sb.Append(wo.GetId().ToString("X"));
                i++;
            }
            AppendSupplyDemand(inv, sb, ';');
        }

        internal static void AppendSupplyDemand(Inventory inv, StringBuilder sb, char separator)
        {
            sb.Append(separator);
            var grs = inv.GetLogisticEntity().GetDemandGroups();
            if (grs != null)
            {
                int j = 0;
                foreach (var s in grs)
                {
                    if (j != 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(s.id);
                    j++;
                }
            }
            sb.Append(separator);
            grs = inv.GetLogisticEntity().GetSupplyGroups();
            if (grs != null)
            {
                int j = 0;
                foreach (var s in grs)
                {
                    if (j != 0)
                    {
                        sb.Append(',');
                    }
                    sb.Append(s.id);
                    j++;
                }
            }
            sb.Append(separator);
            sb.Append(inv.GetLogisticEntity().GetPriority());
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

                        var wi = new WorldInventory();
                        wi.id = int.Parse(innerIds[0]);
                        wi.size = int.Parse(innerIds[1]);

                        {
                            var worldObjects = innerIds[2];

                            foreach (var wo in worldObjects.Split(','))
                            {
                                if (wo.Length != 0)
                                {
                                    wi.itemIds.Add(int.Parse(wo, System.Globalization.NumberStyles.HexNumber));
                                }
                            }
                        }

                        {
                            var demandGroups = innerIds[3];
                            foreach (var dg in demandGroups.Split(','))
                            {
                                if (dg.Length != 0)
                                {
                                    wi.demandGroups.Add(dg);
                                }
                            }
                        }
                        {
                            var supplyGroups = innerIds[4];
                            foreach (var sg in supplyGroups.Split(','))
                            {
                                if (sg.Length != 0)
                                {
                                    wi.supplyGroups.Add(sg);
                                }
                            }
                        }
                        wi.priority = int.Parse(innerIds[5]);

                        minv.inventories.Add(wi);
                    }
                    return true;
                } 
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            minv = null;
            return false;
        }

        public override string GetString()
        {
            throw new NotImplementedException();
        }
    }

    internal class WorldInventory
    {
        internal int id;
        internal int size;
        internal int priority;
        internal readonly List<int> itemIds = new();
        internal readonly List<string> demandGroups = new();
        internal readonly List<string> supplyGroups = new();
    }
}
