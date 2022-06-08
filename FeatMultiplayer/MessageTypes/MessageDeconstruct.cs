using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageDeconstruct : MessageStringProvider
    {
        internal int id;
        internal readonly List<int> itemIds = new();

        internal static bool TryParse(string str, out MessageDeconstruct md)
        {
            if (MessageHelper.TryParseMessage("Deconstruct|", str, 3, out var parameters))
            {
                try
                {
                    md = new();
                    md.id = int.Parse(parameters[1]);
                    if (parameters[2].Length != 0)
                    {
                        foreach (var sid in parameters[2].Split(','))
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

        public string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Deconstruct|").Append(id).Append('|');
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
