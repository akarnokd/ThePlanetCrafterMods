using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    internal class MessageDropWorldObject : MessageBase
    {
        internal int id;
        internal string groupId;
        internal Vector3 position;
        internal float dropSize;

        public static bool TryParse(string str, out MessageDropWorldObject message)
        {
            if (MessageHelper.TryParseMessage("DropWorldObject|", str, 7, out string[] parts))
            {
                try
                {
                    message = new MessageDropWorldObject();
                    message.id = int.Parse(parts[1]);
                    message.groupId = parts[2];
                    message.position.x = float.Parse(parts[3], CultureInfo.InvariantCulture);
                    message.position.y = float.Parse(parts[4], CultureInfo.InvariantCulture);
                    message.position.z = float.Parse(parts[5], CultureInfo.InvariantCulture);

                    message.dropSize = float.Parse(parts[6], CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            message = null;
            return false;
        }

        public override string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("DropWorldObject|");
            sb.Append(id);
            sb.Append('|');
            sb.Append(groupId);
            sb.Append('|');
            sb.Append(position.x.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.y.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.z.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(dropSize.ToString(CultureInfo.InvariantCulture));
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
