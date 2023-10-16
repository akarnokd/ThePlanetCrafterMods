using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    public class MessageDronePosition : MessageBase
    {
        internal int id;
        internal Vector3 position;

        public override string GetString()
        {
            StringBuilder sb = new();
            sb.Append("DronePosition|");
            sb.Append(id);
            sb.Append('|');
            sb.Append(position.x.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.y.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.z.ToString(CultureInfo.InvariantCulture));
            sb.Append('\n');
            return sb.ToString();
        }

        public static bool TryParse(string str, out MessageDronePosition message)
        {
            if (MessageHelper.TryParseMessage("DronePosition|", str, 5, out string[] parts))
            {
                try
                {
                    message = new MessageDronePosition();
                    message.id = int.Parse(parts[1]);
                    message.position.x = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    message.position.y = float.Parse(parts[3], CultureInfo.InvariantCulture);
                    message.position.z = float.Parse(parts[4], CultureInfo.InvariantCulture);

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
