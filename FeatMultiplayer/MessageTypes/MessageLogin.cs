using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageLogin : MessageBase
    {
        internal string user;
        internal string password;

        public override string GetString()
        {
            return "Login|" + user + "|" + password + "\n";
        }

        public static bool TryParse(string str, out MessageLogin result)
        {
            if (MessageHelper.TryParseMessage("Login|", str, 3, out string[] parts))
            {
                result = new MessageLogin();
                result.user = parts[1];
                result.password = parts[2];
                return true;
            }
            result = null;
            return false;
        }
    }
}
