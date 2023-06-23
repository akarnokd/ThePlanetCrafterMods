using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageLaunchTrade : MessageBase
    {
        internal int platformId;

        internal static bool TryParse(string str, out MessageLaunchTrade ml)
        {
            if (MessageHelper.TryParseMessage("LaunchTrade|", str, 2, out var parameters))
            {
                try
                {
                    ml = new();
                    ml.platformId = int.Parse(parameters[1]);
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
            return "LaunchTrade|" + platformId + "\n";
        }
    }
}
