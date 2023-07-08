using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageDeconstructPanel : MessageBase
    {
        /// <summary>
        /// The world object id of the parent structure.
        /// </summary>
        internal int id;
        /// <summary>
        /// The panel index within the structure.
        /// </summary>
        internal int index;

        internal readonly List<int> itemIds = new();

        internal static bool TryParse(string str, out MessageDeconstructPanel md)
        {
            if (MessageHelper.TryParseMessage("DeconstructPanel|", str, 4, out var parameters))
            {
                try
                {
                    md = new();
                    md.id = int.Parse(parameters[1]);
                    md.index = int.Parse(parameters[2]);
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
            sb.Append("DeconstructPanel|").Append(id).Append('|').Append(index).Append('|');
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
