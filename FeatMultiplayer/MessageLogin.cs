using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageLogin : MessageStringProvider
    {
        internal string user;
        internal string password;

        public string GetString()
        {
            return "Login|" + user + "|" + password + "\n";
        }

        public static bool TryParse(string str, out MessageLogin result)
        {
            if (MessageHelper.TryParseMessage("Login|", str, out string[] parts))
            {
                if (parts.Length == 3)
                {
                    result = new MessageLogin();
                    result.user = parts[1];
                    result.password = parts[2];
                    return true;
                }
            }
            result = null;
            return false;
        }
    }
}
