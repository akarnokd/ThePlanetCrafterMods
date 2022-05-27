using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageHelper
    {
        internal static bool TryParseMessage(string type, string str, out string[] parameters)
        {
            if (str.StartsWith(type))
            {
                parameters = str.Split('|');
                return true;
            }
            parameters = null;
            return false;
        }
    }
}
