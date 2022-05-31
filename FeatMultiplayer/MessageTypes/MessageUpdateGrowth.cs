using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageUpdateGrowth : MessageStringProvider
    {
        internal int machineId;
        internal float growth;
        internal int vegetableId;

        internal static bool TryParse(string str, out MessageUpdateGrowth mug)
        {
            if (MessageHelper.TryParseMessage("UpdateGrowth|", str, 4, out var parameters))
            {
                try
                {
                    mug = new MessageUpdateGrowth();
                    mug.machineId = int.Parse(parameters[1]);
                    mug.growth = float.Parse(parameters[2], CultureInfo.InvariantCulture);
                    mug.vegetableId = int.Parse(parameters[3]);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mug = null;
            return false;
        }

        public string GetString()
        {
            return "UpdateGrowth|" + machineId + "|" + growth.ToString(CultureInfo.InvariantCulture) + "|" + vegetableId + "\n";
        }
    }
}
