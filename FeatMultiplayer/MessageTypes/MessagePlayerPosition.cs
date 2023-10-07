using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    public class MessagePlayerPosition : MessageBase
    {
        internal Vector3 position;
        internal Quaternion rotation;
        internal int lightMode;
        internal string clientName;
        internal Vector3 miningPosition;
        internal int walkMode;

        public override string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("PlayerPosition|");
            sb.Append(position.x.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.y.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(position.z.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(rotation.x.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(rotation.y.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(rotation.z.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(rotation.w.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(lightMode);
            sb.Append('|');
            sb.Append(clientName);
            sb.Append('|');
            sb.Append(miningPosition.x.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(miningPosition.y.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(miningPosition.z.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(walkMode);
            sb.Append('\n');
            return sb.ToString();
        }

        public static bool TryParse(string str, out MessagePlayerPosition message)
        {
            if (MessageHelper.TryParseMessage("PlayerPosition|", str, 14, out string[] parts))
            {
                try
                {
                    message = new MessagePlayerPosition();
                    message.position.x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    message.position.y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    message.position.z = float.Parse(parts[3], CultureInfo.InvariantCulture);

                    message.rotation.x = float.Parse(parts[4], CultureInfo.InvariantCulture);
                    message.rotation.y = float.Parse(parts[5], CultureInfo.InvariantCulture);
                    message.rotation.z = float.Parse(parts[6], CultureInfo.InvariantCulture);
                    message.rotation.w = float.Parse(parts[7], CultureInfo.InvariantCulture);

                    message.lightMode = int.Parse(parts[8]);
                    message.clientName = parts[9];

                    message.miningPosition.x = float.Parse(parts[10], CultureInfo.InvariantCulture);
                    message.miningPosition.y = float.Parse(parts[11], CultureInfo.InvariantCulture);
                    message.miningPosition.z = float.Parse(parts[12], CultureInfo.InvariantCulture);

                    message.walkMode = int.Parse(parts[13]);

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
