using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageMicrochipUnlock : MessageStringProvider
    {
        internal string groupId;

        internal static bool TryParse(string str, out MessageMicrochipUnlock mmu)
        {
            if (MessageHelper.TryParseMessage("MicrochipUnlock|", str, out var parameters))
            {
                if (parameters.Length == 2)
                {
                    mmu = new MessageMicrochipUnlock();
                    mmu.groupId = parameters[1];
                    return true;
                }
                else
                {
                    Plugin.LogError("MicrochipUnlock.Length = " + parameters.Length);
                }
            }
            mmu = null;
            return false;
        }

        public string GetString()
        {
            return "MicrochipUnlock|" + groupId + "\n";
        }
    }
}
