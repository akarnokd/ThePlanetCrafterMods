using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageUpdateColor : MessageBase
    {
        internal int id;
        internal Color color;

        internal static bool TryParse(string str, out MessageUpdateColor muc)
        {
            if (MessageHelper.TryParseMessage("UpdateColor|", str, out var parameters))
            {
                if (parameters.Length == 3)
                {
                    try
                    {
                        muc = new MessageUpdateColor();
                        muc.id = int.Parse(parameters[1]);
                        muc.color = MessageHelper.StringToColor(parameters[2]);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError(ex);
                    }
                }
                else
                {
                    Plugin.LogError("MessageUpdateText.Length = " + parameters.Length);
                }
            }
            muc = null;
            return false;
        }

        public override string GetString()
        {
            return "UpdateColor|" + id + "|" + MessageHelper.ColorToString(color) + "\n";
        }
    }
}
