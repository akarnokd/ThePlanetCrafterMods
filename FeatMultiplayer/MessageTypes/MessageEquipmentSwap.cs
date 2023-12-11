using System;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageEquipmentSwap : MessageBase
    {
        internal static bool TryParse(string str, out MessageEquipmentSwap mis)
        {
            if (MessageHelper.TryParseMessage("EquipmentSwap|", str, 2, out var parameters))
            {
                try
                {
                    mis = new();
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mis = null;
            return false;
        }

        public override string GetString()
        {
            return "EquipmentSwap|\n";
        }
    }
}
