using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessagePlayerWelcome : MessageBase
    {
        internal Version multiplayerModVersion;
        internal readonly Dictionary<string, Version> modVersions = new();
        internal string hostDisplayName;
        public static bool TryParse(string str, out MessagePlayerWelcome mpw)
        {
            if (MessageHelper.TryParseMessage("Welcome", str, 4, out var parameters))
            {
                try
                {
                    Plugin.LogInfo(str);
                    mpw = new();
                    mpw.multiplayerModVersion = new Version(parameters[1]);

                    foreach (var kv in parameters[2].Split(';'))
                    {
                        var kvv = kv.Split('=');
                        mpw.modVersions[kvv[0]] = new Version(kvv[1]);
                    }
                    mpw.hostDisplayName = MessageHelper.DecodeText(parameters[3]);

                    return true;
                } 
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mpw = null;
            return false;
        }

        public override string GetString()
        {
            StringBuilder sb = new();
            sb.Append("Welcome|").Append(multiplayerModVersion).Append('|');
            int j = 0;
            foreach (var m in modVersions)
            {
                if (j != 0)
                {
                    sb.Append(';');
                }
                sb.Append(m.Key).Append('=').Append(m.Value);
                j++;
            }
            sb.Append('|');
            MessageHelper.EncodeText(hostDisplayName, sb);
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
