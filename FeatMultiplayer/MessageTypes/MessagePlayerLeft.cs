using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessagePlayerLeft : MessageBase
    {
        public string playerName;

        public static bool TryParse(string str, out MessagePlayerLeft msg)
        {
            if (MessageHelper.TryParseMessage("PlayerLeft|", str, 2, out var parameters))
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
            return "PlayerLeft|" + playerName + "\n";
        }
    }
}
