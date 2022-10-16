using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessagePlayerWelcome : MessageBase
    {
        public static bool TryParse(string str, out MessagePlayerWelcome mpw)
        {
            if (str == "Welcome")
            {
                mpw = new();
                return true;
            }
            mpw = null;
            return false;
        }

        public override string GetString()
        {
            return "Welcome\n";
        }
    }
}
