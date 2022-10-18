using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageEmote : MessageBase
    {
        internal string playerName;

        internal string emoteId;

        public override string GetString()
        {
            return "Emote|" + playerName + "|" + emoteId + "\n";
        }

        public static bool TryParse(string str, out MessageEmote result)
        {
            if (MessageHelper.TryParseMessage("Emote|", str, 3, out string[] parts))
            {
                result = new MessageEmote();
                result.playerName = parts[1];
                result.emoteId = parts[2];
                return true;
            }
            result = null;
            return false;
        }
    }
}
