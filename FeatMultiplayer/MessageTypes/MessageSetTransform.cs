using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    internal class MessageSetTransform : MessageStringProvider
    {
        internal int id;
        internal Vector3 position;
        internal Quaternion rotation;
        /// <summary>
        /// 1 - WorldObject only, 2 - GameObject only, 3 both
        /// </summary>
        internal Mode mode;

        internal enum Mode
        {
            WorldObjectOnly,
            GameObjectOnly,
            Both
        }

        public static bool TryParse(string str, out MessageSetTransform message)
        {
            if (MessageHelper.TryParseMessage("SetTransform|", str, out string[] parts))
            {
                if (parts.Length == 10)
                {
                    message = new MessageSetTransform();
                    try
                    {
                        message.id = int.Parse(parts[1]);
                        message.position.x = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        message.position.y = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        message.position.z = float.Parse(parts[4], CultureInfo.InvariantCulture);

                        message.rotation.x = float.Parse(parts[5], CultureInfo.InvariantCulture);
                        message.rotation.y = float.Parse(parts[6], CultureInfo.InvariantCulture);
                        message.rotation.z = float.Parse(parts[7], CultureInfo.InvariantCulture);
                        message.rotation.w = float.Parse(parts[8], CultureInfo.InvariantCulture);
                        message.mode = (Mode)int.Parse(parts[9]);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError(ex);
                    }
                }
                else
                {
                    Plugin.LogInfo("MessageSetTransform missing data: " + parts.Length);
                }
            }
            message = null;
            return false;
        }

        public string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("SetTransform|");
            sb.Append(id);
            sb.Append('|');
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
            sb.Append((int)mode);
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
