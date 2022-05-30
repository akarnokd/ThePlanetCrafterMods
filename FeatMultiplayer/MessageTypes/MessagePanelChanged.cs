using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessagePanelChanged : MessageStringProvider
    {
        internal int itemId;
        internal int panelId;
        internal int panelType;
        internal string panelGroupId;

        internal static bool TryParse(string str, out MessagePanelChanged mpc)
        {
            if (MessageHelper.TryParseMessage("PanelChanged|", str, out var parameters))
            {
                if (parameters.Length == 5)
                {
                    try
                    {
                        mpc = new MessagePanelChanged();
                        mpc.itemId = int.Parse(parameters[1]);
                        mpc.panelId = int.Parse(parameters[2]);
                        mpc.panelType = int.Parse(parameters[3]);
                        mpc.panelGroupId = parameters[4];
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError(ex);
                    }
                }
            }
            mpc = null;
            return false;
        }

        public string GetString()
        {
            return "PanelChanged|" + itemId + "|" + panelId + "|" + panelType + "|" + panelGroupId + "\n";
        }
    }
}
