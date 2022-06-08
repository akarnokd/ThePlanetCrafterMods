using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public class MessagePlayerPosition : MessageStringProvider
    {
        internal Vector3 position;
        internal Quaternion rotation;
        internal int lightMode;

        public string GetString()
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
            sb.Append('\n');
            return sb.ToString();
        }

        public static bool TryParse(string str, out MessagePlayerPosition message)
        {
            if (MessageHelper.TryParseMessage("PlayerPosition|", str, 9, out string[] parts))
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
