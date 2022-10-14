using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public class MessageMovePlayer : MessageStringProvider
    {
        internal Vector3 position;

        public string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("MovePlayer|");
            sb.Append(position.x.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.y.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.z.ToString(CultureInfo.InvariantCulture));
            sb.Append('\n');
            return sb.ToString();
        }

        public static bool TryParse(string str, out MessageMovePlayer message)
        {
            if (MessageHelper.TryParseMessage("MovePlayer|", str, 4, out string[] parts))
            {
                try
                {
                    message = new MessageMovePlayer();
                    message.position.x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    message.position.y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    message.position.z = float.Parse(parts[3], CultureInfo.InvariantCulture);
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
    }
}
