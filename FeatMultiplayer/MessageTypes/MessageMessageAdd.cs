using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageMessageAdd : MessageBase
    {
        internal string messageId;

        internal static bool TryParse(string str, out MessageMessageAdd mma)
        {
            if (MessageHelper.TryParseMessage("MessageAdd|", str, 2, out var parameters))
            {
                mma = new();
                mma.messageId = parameters[1];
                return true;
            }
            mma = null;
            return false;
        }

        public override string GetString()
        {
            return "MessageAdd|" + messageId + "\n";
        }
    }
}
