using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    internal class MessagePlaceConstructible : MessageStringProvider
    {
        internal string groupId;
        internal Vector3 position;
        internal Quaternion rotation;

        public string GetString()
        {
            return "PlaceConstructible|" + groupId
                + "|" + DataTreatments.Vector3ToString(position)
                + "|" + DataTreatments.QuaternionToString(rotation) + "\n";
        }

        internal static bool TryParse(string str, out MessagePlaceConstructible mpc)
        {
            if (MessageHelper.TryParseMessage("PlaceConstructible|", str, out var parameters))
            {
                if (parameters.Length == 4)
                {
                    try
                    {
                        mpc = new MessagePlaceConstructible();
                        mpc.groupId = parameters[1];
                        mpc.position = DataTreatments.StringToVector3(parameters[2]);
                        mpc.rotation = DataTreatments.StringToQuaternion(parameters[3]);
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
    }
}
