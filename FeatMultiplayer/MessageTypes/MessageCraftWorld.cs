using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    internal class MessageCraftWorld : MessageStringProvider
    {
        internal string groupId;
        internal Vector3 position;
        internal float craftTime;

        internal static bool TryParse(string str, out MessageCraftWorld mc)
        {
            if (MessageHelper.TryParseMessage("CraftWorld|", str, 4, out var parameters))
            {
                try
                {
                    mc = new MessageCraftWorld();
                    mc.groupId = parameters[1];
                    mc.position = DataTreatments.StringToVector3(parameters[2]);
                    mc.craftTime = float.Parse(parameters[3], CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mc = null;
            return false;
        }

        public string GetString()
        {
            return "CraftWorld|" + groupId
                + "|" + DataTreatments.Vector3ToString(position)
                + "|" + craftTime.ToString(CultureInfo.InvariantCulture)
                + "\n";
        }
    }
}
