using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageDeconstruct : MessageBase
    {
        internal int id;
        internal string groupId;
        internal readonly List<int> itemIds = new();

        internal static bool TryParse(string str, out MessageDeconstruct md)
        {
            if (MessageHelper.TryParseMessage("Deconstruct|", str, 4, out var parameters))
            {
                try
                {
                    md = new();
                    md.id = int.Parse(parameters[1]);
                    md.groupId = parameters[2];
                    if (parameters[3].Length != 0)
                    {
                        foreach (var sid in parameters[3].Split(','))
                        {
                            md.itemIds.Add(int.Parse(sid));
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            md = null;
            return false;
        }

        public override string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Deconstruct|").Append(id).Append('|').Append(groupId).Append('|');
            for (int i = 0; i < itemIds.Count; i++)
            {
                int id = itemIds[i];
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(id);
            }
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
