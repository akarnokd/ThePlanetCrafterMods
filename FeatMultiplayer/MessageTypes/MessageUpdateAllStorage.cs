using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageUpdateAllStorage : MessageBase
    {
        internal readonly Dictionary<string, string> storage = new();

        public override string GetString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("UpdateAllStorage|");
            sb.Append(storage.Count);
            foreach (var kv in storage)
            {
                sb.Append('|');
                sb.Append(StorageHelper.StorageEncodeString(kv.Key));
                sb.Append('|');
                sb.Append(StorageHelper.StorageEncodeString(kv.Value));
            }
            sb.Append('\n');
            return sb.ToString();
        }

        public static bool TryParse(string str, out MessageUpdateAllStorage result)
        {
            if (MessageHelper.TryParseMessage("UpdateAllStorage|", str, out string[] parts))
            {
                if (parts.Length < 2 || parts.Length % 2 != 0)
                {
                    Plugin.LogError("UpdateAllStorage: must be at least 2 parts and length even: " + parts.Length);
                }
                else
                {
                    result = new MessageUpdateAllStorage();
                    int cnt = int.Parse(parts[1]) * 2 + 2;
                    for (int i = 2; i < cnt; i += 2)
                    {
                        var key = StorageHelper.StorageDecodeString(parts[i]);
                        var value = StorageHelper.StorageDecodeString(parts[i + 1]);
                        result.storage[key] = value;
                    }
                    return true;
                }
            }
            result = null;
            return false;
        }
    }
}
