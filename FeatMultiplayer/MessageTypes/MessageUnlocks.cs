using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageUnlocks : MessageBase
    {
        internal List<string> groupIds;

        internal static bool TryParse(string str, out MessageUnlocks mu)
        {
            if (MessageHelper.TryParseMessage("Unlocks|", str, out var parameters))
            {
                if (parameters.Length >= 2)
                {
                    mu = new MessageUnlocks();

                    if (parameters[1].Length > 0)
                    {
                        mu.groupIds = new List<string>(parameters.Length);
                        for (int i = 1; i < parameters.Length; i++)
                        {
                            mu.groupIds.Add(parameters[i]);
                        }
                    }
                    else
                    {
                        mu.groupIds = new List<string>();
                    }
                    return true;
                }
                else
                {
                    Plugin.LogError("Unlocks.Length = " + parameters.Length);
                }
            }
            mu = null;
            return false;
        }

        public override string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Unlocks");
            if (groupIds != null && groupIds.Count > 0)
            {
                foreach (string id in groupIds)
                {
                    sb.Append('|');
                    sb.Append(id);
                }
            }
            else
            {
                sb.Append('|');
            }
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
