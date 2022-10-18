using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageMeteorEvent : MessageBase
    {
        internal int eventIndex;
        internal float startTime;
        internal bool isRocket;

        internal static bool TryParse(string str, out MessageMeteorEvent mme)
        {
            if (MessageHelper.TryParseMessage("MeteorEvent|", str, 4, out var parameters))
            {
                try
                {
                    mme = new();
                    mme.eventIndex = int.Parse(parameters[1]);
                    mme.startTime = float.Parse(parameters[2], CultureInfo.InvariantCulture);
                    mme.isRocket = "1" == parameters[3];
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mme = null;
            return false;
        }
        public override string GetString()
        {
            return "MeteorEvent|" + eventIndex
                + "|" + startTime.ToString(CultureInfo.InvariantCulture)
                + "|" + (isRocket ? 1 : 0)
                + "\n";
        }
    }
}
