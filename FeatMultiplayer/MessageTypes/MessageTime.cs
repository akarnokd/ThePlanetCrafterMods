using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageTime : MessageStringProvider
    {
        internal float time;

        internal static bool TryParse(string str, out MessageTime mt)
        {
            if (MessageHelper.TryParseMessage("Time|", str, 2, out var parameters))
            {
                try
                {
                    mt = new();
                    mt.time = float.Parse(parameters[1], CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mt = null;
            return false;
        }

        public string GetString()
        {
            return "Time|" + time.ToString(CultureInfo.InvariantCulture) + "\n";
        }
    }
}
