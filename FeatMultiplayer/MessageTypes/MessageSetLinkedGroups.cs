using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    internal class MessageSetLinkedGroups : MessageBase
    {
        internal int id;
        internal List<string> groupIds;

        public static bool TryParse(string str, out MessageSetLinkedGroups message)
        {
            if (MessageHelper.TryParseMessage("SetLinkedGroups|", str, out string[] parts))
            {
                if (parts.Length >= 2)
                {
                    message = new MessageSetLinkedGroups();
                    try
                    {
                        message.id = int.Parse(parts[1]);
                        if (parts.Length > 2)
                        {
                            message.groupIds = new();
                            for (int i = 2; i < parts.Length; i++)
                            {
                                message.groupIds.Add(parts[i]);
                            }
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError(ex);
                    }
                }
                else
                {
                    Plugin.LogInfo("MessageSetLinkedGroups: " + parts.Length);
                }
            }
            message = null;
            return false;
        }

        public override string GetString()
        {
            StringBuilder sb = new();
            sb.Append("SetLinkedGroups|");
            sb.Append(id);
            if (groupIds != null && groupIds.Count != 0)
            {
                foreach (var gid in groupIds)
                {
                    sb.Append('|');
                    sb.Append(gid);
                }
            }
            sb.Append('\n');
            return sb.ToString();
        }
    }
}
