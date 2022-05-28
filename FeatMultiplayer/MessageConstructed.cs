using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageConstructed
    {
        internal MessageWorldObject worldObject;
        public static bool TryParse(string str, out MessageConstructed mc)
        {
            if (MessageHelper.TryParseMessage("Constructed|", str, out var parameters))
            {
                try
                {
                    mc = new MessageConstructed();
                    return MessageWorldObject.TryParse(parameters, 1, out mc.worldObject);
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mc = null;
            return false;
        }
    }
}
