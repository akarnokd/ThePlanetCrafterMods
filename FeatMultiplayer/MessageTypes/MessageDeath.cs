using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageDeath : MessageBase
    {
        internal Vector3 position;
        internal int chestId;
        internal static bool TryParse(string str, out MessageDeath md)
        {
            if (MessageHelper.TryParseMessage("Death|", str, 3, out var parameters))
            {
                try
                {
                    md = new();
                    md.position = DataTreatments.StringToVector3(parameters[1]);
                    md.chestId = int.Parse(parameters[2]);
                    return true;
                } catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            md = null;
            return false;
        }

        public override string GetString()
        {
            return "Death|" + DataTreatments.Vector3ToString(position)
                + "|" + chestId
                + "\n";
        }
    }
}
