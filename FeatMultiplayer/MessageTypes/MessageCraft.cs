using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageCraft : MessageStringProvider
    {
        internal string groupId;

        internal static bool TryParse(string str, out MessageCraft mc)
        {
            if (MessageHelper.TryParseMessage("Craft|", str, 2, out var parameters))
            {
                mc = new MessageCraft();
                mc.groupId = parameters[1];
                return true;
            }
            mc = null;
            return false;
        }

        public string GetString()
        {
            return "Craft|" + groupId + "\n";
        }
    }
}
