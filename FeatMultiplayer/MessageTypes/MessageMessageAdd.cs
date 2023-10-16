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

        internal bool showPopup;

        internal static bool TryParse(string str, out MessageMessageAdd mma)
        {
            if (MessageHelper.TryParseMessage("MessageAdd|", str, 3, out var parameters))
            {
                mma = new();
                mma.messageId = parameters[1];
                mma.showPopup = "1" == parameters[2];
                return true;
            }
            mma = null;
            return false;
        }

        public override string GetString()
        {
            return "MessageAdd|" + messageId + "|" + (showPopup ? "1" : "0") + "\n";
        }
    }
}
