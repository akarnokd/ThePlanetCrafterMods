using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageGrowRemove : MessageBase
    {
        internal int machineId;
        internal int spawnId;

        internal static bool TryParse(string str, out MessageGrowRemove mgr)
        {
            if (MessageHelper.TryParseMessage("GrowRemove|", str, 3, out var parameters))
            {
                try
                {
                    mgr = new();
                    mgr.machineId = int.Parse(parameters[1]);
                    mgr.spawnId = int.Parse(parameters[2]);

                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mgr = null;
            return false;
        }

        public override string GetString()
        {
            return "GrowRemove|"
                + machineId
                + "|" + spawnId
                + "\n";
        }
    }
}
