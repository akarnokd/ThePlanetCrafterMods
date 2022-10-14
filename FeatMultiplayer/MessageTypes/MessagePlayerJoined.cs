using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessagePlayerJoined : MessageBase
    {
        public string playerName;

        public static bool TryParse(string str, out MessagePlayerJoined msg)
        {
            if (MessageHelper.TryParseMessage("PlayerJoined|", str, 2, out var parameters))
            {
                msg = new();
                msg.playerName = parameters[1];
                return true;
            }
            msg = null;
            return false;
        }

        public override string GetString()
        {
            return "PlayerJoined|" + playerName + "\n";
        }
    }
}
