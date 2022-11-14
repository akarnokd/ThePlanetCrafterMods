using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageStoryEvents : MessageBase
    {
        internal List<string> eventIds;

        internal static bool TryParse(string str, out MessageStoryEvents mu)
        {
            if (MessageHelper.TryParseMessage("StoryEvents|", str, out var parameters))
            {
                if (parameters.Length >= 2)
                {
                    mu = new MessageStoryEvents();

                    if (parameters[1].Length > 0)
                    {
                        mu.eventIds = new List<string>(parameters.Length);
                        for (int i = 1; i < parameters.Length; i++)
                        {
                            mu.eventIds.Add(parameters[i]);
                        }
                    }
                    else
                    {
                        mu.eventIds = new List<string>();
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
            sb.Append("StoryEvents");
            if (eventIds != null && eventIds.Count > 0)
            {
                foreach (string id in eventIds)
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
