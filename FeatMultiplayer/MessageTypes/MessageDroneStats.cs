using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    public class MessageDroneStats : MessageBase
    {
        internal int supplyCount;
        internal int demandCount;

        public override string GetString()
        {
            StringBuilder sb = new();
            sb.Append("DroneStats|");
            sb.Append(supplyCount);
            sb.Append('|');
            sb.Append(demandCount);
            sb.Append('\n');
            return sb.ToString();
        }

        public static bool TryParse(string str, out MessageDroneStats message)
        {
            if (MessageHelper.TryParseMessage("DroneStats|", str, 3, out string[] parts))
            {
                try
                {
                    message = new MessageDroneStats();
                    message.supplyCount = int.Parse(parts[1]);
                    message.demandCount = int.Parse(parts[2]);

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
