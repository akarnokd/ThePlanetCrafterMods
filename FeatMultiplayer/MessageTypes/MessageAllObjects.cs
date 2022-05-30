using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageAllObjects
    {
        internal List<MessageWorldObject> worldObjects = new();
        internal static void AppendWorldObject(StringBuilder sb, char separator, WorldObject wo, bool makeGrabable)
        {
            sb.Append(wo.GetId());  // 0
            sb.Append(separator);
            sb.Append(wo.GetGroup().GetId());  // 1
            sb.Append(separator);
            sb.Append(wo.GetLinkedInventoryId());  // 2
            sb.Append(separator);
            sb.Append(GroupsHandler.GetGroupsStringIds(wo.GetLinkedGroups())); // 3
            sb.Append(separator);
            if (wo.GetIsPlaced())
            {
                sb.Append(DataTreatments.Vector3ToString(wo.GetPosition())); // 4
                sb.Append(separator);
                sb.Append(DataTreatments.QuaternionToString(wo.GetRotation())); // 5
                sb.Append(separator);
            }
            else
            {
                sb.Append(separator);
                sb.Append(separator);
            }
            sb.Append(DataTreatments.ColorToString(wo.GetColor())); // 6
            sb.Append(separator);
            sb.Append(wo.GetText()); // 7
            sb.Append(separator);
            sb.Append(DataTreatments.IntListToString(wo.GetPanelsId())); // 8
            sb.Append(separator);
            sb.Append(wo.GetGrowth().ToString(CultureInfo.InvariantCulture)); // 9
            sb.Append(separator);
            sb.Append(makeGrabable ? 1 : 0);
        }
        public static bool TryParse(string str, out MessageAllObjects mc)
        {
            if (MessageHelper.TryParseMessage("AllObjects|", str, out var parameters))
            {
                try
                {
                    mc = new MessageAllObjects();

                    for (int i = 1; i < parameters.Length; i++)
                    {
                        if (parameters[i].Length == 0)
                        {
                            continue;
                        }
                        string[] objs = parameters[i].Split(';');
                        if (MessageWorldObject.TryParse(objs, 0, out var mwo))
                        {
                            mc.worldObjects.Add(mwo);
                        }
                    }

                    return true;
                } 
                catch (Exception ex)
                {
                    Plugin.LogError(str +"\r\n\r\n" + ex);
                }
            }
            mc = null;
            return false;
        }
    }
}
