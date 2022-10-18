using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageMined : MessageBase
    {
        internal int id;
        internal string groupId;

        internal static bool TryParse(string str, out MessageMined mm)
        {
            if (MessageHelper.TryParseMessage("Mined|", str, 3, out var parameters))
            {
                try
                {
                    mm = new();
                    mm.id = int.Parse(parameters[1]);
                    mm.groupId = parameters[2];
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mm = null;
            return false;
        }

        public override string GetString()
        {
            return "Mined|" + id + "|" + groupId + "\n";
        }
    }
}
