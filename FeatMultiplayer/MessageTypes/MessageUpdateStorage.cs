using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageUpdateStorage : MessageBase
    {
        internal string key;
        internal string value;

        public override string GetString()
        {
            return "UpdateStorage|" + StorageHelper.StorageEncodeString(key) + "|" + StorageHelper.StorageEncodeString(value) + "\n";
        }

        public static bool TryParse(string str, out MessageUpdateStorage result)
        {
            if (MessageHelper.TryParseMessage("UpdateStorage|", str, 3, out string[] parts))
            {
                result = new MessageUpdateStorage();
                result.key = StorageHelper.StorageDecodeString(parts[1]);
                result.value = StorageHelper.StorageDecodeString(parts[2]);
                return true;
            }
            result = null;
            return false;
        }
    }
}
