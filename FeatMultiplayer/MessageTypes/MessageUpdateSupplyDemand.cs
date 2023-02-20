using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageUpdateSupplyDemand : MessageBase
    {
        internal int inventoryId;
        internal readonly List<string> demandGroups = new();
        internal readonly List<string> supplyGroups = new();

        internal static bool TryParse(string str, out MessageUpdateSupplyDemand msg)
        {
            if (MessageHelper.TryParseMessage("UpdateSupplyDemand|", str, 4, out var parameters))
            {
                try
                {
                    msg = new MessageUpdateSupplyDemand();
                    msg.inventoryId = int.Parse(parameters[1]);

                    var demandGroups = parameters[2];
                    foreach (var dg in demandGroups.Split(','))
                    {
                        if (dg.Length != 0)
                        {
                            msg.demandGroups.Add(dg);
                        }
                    }
                    var supplyGroups = parameters[3];
                    foreach (var sg in supplyGroups.Split(','))
                    {
                        if (sg.Length != 0)
                        {
                            msg.supplyGroups.Add(sg);
                        }
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            msg = null;
            return false;
        }

        public override string GetString()
        {
            StringBuilder sb = new();
            sb.Append("UpdateSupplyDemand|");
            sb.Append(inventoryId);
            sb.Append('|');
            sb.Append(string.Join(",", demandGroups));
            sb.Append('|');
            sb.Append(string.Join(",", supplyGroups));
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
