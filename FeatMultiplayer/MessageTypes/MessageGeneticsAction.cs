using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageGeneticsAction : MessageStringProvider
    {
        internal int machineId;
        internal string groupId;

        internal static bool TryParse(string str, out MessageGeneticsAction mga) 
        {
            if (MessageHelper.TryParseMessage("GeneticsAction|", str, 3, out var parameters))
            {
                try
                {
                    mga = new();
                    mga.machineId = int.Parse(parameters[1]);
                    mga.groupId = parameters[2];
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mga = null;
            return false;
        }

        public string GetString()
        {
            return "GeneticsAction|" + machineId + "|" + groupId + "\n";
        }
    }

    internal enum GeneticsAction
    {
        Cancel,
        Analyze,
        Sequence
    }
}
