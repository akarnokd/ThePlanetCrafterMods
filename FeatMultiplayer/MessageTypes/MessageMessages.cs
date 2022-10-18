using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageMessages : MessageBase
    {
        internal readonly List<string> messages = new();

        internal static bool TryParse(string str, out MessageMessages mm)
        {
            if (MessageHelper.TryParseMessage("Messages|", str, 2, out var parameters))
            {
                mm = new MessageMessages();
                if (parameters[1].Length != 0)
                {
                    mm.messages.AddRange(parameters[1].Split(','));
                }
                return true;
            }
            mm = null;
            return false;
        }

        public override string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Messages|");
            for (int i = 0; i < messages.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(messages[i]);
            }
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
