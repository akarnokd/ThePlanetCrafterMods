using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageGameMode : MessageBase
    {
        internal int modeIndex;

        internal static bool TryParse(string str, out MessageGameMode mgm)
        {
            if (MessageHelper.TryParseMessage("GameMode|", str, 2, out var parameters))
            {
                try
                {
                    mgm = new();
                    mgm.modeIndex = int.Parse(parameters[1]);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mgm = null;
            return false;
        }

        public override string GetString()
        {
            return "GameMode|" + modeIndex + "\n";
        }
    }
}
