using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageLaunch : MessageBase
    {
        internal int rocketId;

        internal static bool TryParse(string str, out MessageLaunch ml)
        {
            if (MessageHelper.TryParseMessage("Launch|", str, 2, out var parameters))
            {
                try
                {
                    ml = new();
                    ml.rocketId = int.Parse(parameters[1]);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            ml = null;
            return false;
        }

        public override string GetString()
        {
            return "Launch|" + rocketId + "\n";
        }
    }
}
