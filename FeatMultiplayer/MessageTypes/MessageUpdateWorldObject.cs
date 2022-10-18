using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageUpdateWorldObject : MessageBase
    {
        internal MessageWorldObject worldObject;

        public static bool TryParse(string str, out MessageUpdateWorldObject mc)
        {
            if (MessageHelper.TryParseMessage("UpdateWorldObject|", str, out var parameters))
            {
                try
                {
                    mc = new MessageUpdateWorldObject();
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

        public override string GetString()
        {
            throw new NotImplementedException();
        }
    }
}
