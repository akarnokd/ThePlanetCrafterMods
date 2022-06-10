using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageGrab : MessageStringProvider
    {
        internal int id;
        internal string groupId;

        internal static bool TryParse(string str, out MessageGrab mg)
        {
            if (MessageHelper.TryParseMessage("Grab|", str, 3, out var parameters))
            {
                try
                {
                    mg = new MessageGrab();
                    mg.id = int.Parse(parameters[1]);
                    mg.groupId = parameters[2];
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mg = null;
            return false;
        }

        public string GetString()
        {
            return "Grab|" + id + "|" + groupId + "\n";
        }
    }
}
