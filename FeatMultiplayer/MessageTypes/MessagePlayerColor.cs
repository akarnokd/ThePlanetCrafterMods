using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessagePlayerColor : MessageBase
    {
        internal string playerName;

        internal Color color;

        internal static bool TryParse(string str, out MessagePlayerColor mut)
        {
            if (MessageHelper.TryParseMessage("PlayerColor|", str, 3, out var parameters))
            {
                try
                {
                    mut = new MessagePlayerColor();
                    mut.playerName = parameters[1];
                    mut.color = MessageHelper.StringToColor(parameters[2]);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mut = null;
            return false;
        }

        public override string GetString()
        {
            return "PlayerColor|" + playerName + "|" + MessageHelper.ColorToString(color) + "\n";
        }
    }
}
