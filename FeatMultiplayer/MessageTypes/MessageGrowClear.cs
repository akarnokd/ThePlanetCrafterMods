using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageGrowClear : MessageStringProvider
    {
        internal int machineId;

        internal static bool TryParse(string str, out MessageGrowClear mgc)
        {
            if (MessageHelper.TryParseMessage("GrowClear|", str, 2, out var parameters))
            {
                try
                {
                    mgc = new();
                    mgc.machineId = int.Parse(parameters[1]);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mgc = null;
            return false;
        }

        public string GetString()
        {
            return "GrowClear|" + machineId + "\n";
        }
    }
}
