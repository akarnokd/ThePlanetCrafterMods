using SpaceCraft;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageWorldObject
    {
        public int id;
        public string groupId;
        public int inventoryId;
        public string[] groupIds;
        public Vector3 position;
        public Quaternion rotation;
        public Color color;
        public string text;
        public List<int> panelIds;
        public float growth;
        public bool makeGrabable;

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
            sb.Append(MessageHelper.ColorToString(wo.GetColor())); // 6
            sb.Append(separator);
            sb.Append(wo.GetText()); // 7
            sb.Append(separator);
            sb.Append(DataTreatments.IntListToString(wo.GetPanelsId())); // 8
            sb.Append(separator);
            sb.Append(wo.GetGrowth().ToString(CultureInfo.InvariantCulture)); // 9
            sb.Append(separator);
            sb.Append(makeGrabable ? 1 : 0); // 10
        }

        internal static void AppendWorldObject(StringBuilder sb, char separator, MessageWorldObject wo, bool makeGrabable)
        {
            sb.Append(wo.id);  // 0
            sb.Append(separator);
            sb.Append(wo.groupId);  // 1
            sb.Append(separator);
            sb.Append(wo.inventoryId);  // 2
            sb.Append(separator);
            if (wo.groupIds != null && wo.groupIds.Length != 0)
            {
                for (int i = 0; i < wo.groupIds.Length; i++)
                {
                    if (i != 0)
                    {
                        sb.Append(',');
                    }
                    string g = wo.groupIds[i];
                    sb.Append(g);
                }
            }
            sb.Append(separator);
            if (wo.position != Vector3.zero)
            {
                sb.Append(DataTreatments.Vector3ToString(wo.position)); // 4
                sb.Append(separator);
                sb.Append(DataTreatments.QuaternionToString(wo.rotation)); // 5
                sb.Append(separator);
            }
            else
            {
                sb.Append(separator);
                sb.Append(separator);
            }
            sb.Append(MessageHelper.ColorToString(wo.color)); // 6
            sb.Append(separator);
            sb.Append(wo.text); // 7
            sb.Append(separator);
            sb.Append(DataTreatments.IntListToString(wo.panelIds)); // 8
            sb.Append(separator);
            sb.Append(wo.growth.ToString(CultureInfo.InvariantCulture)); // 9
            sb.Append(separator);
            sb.Append(makeGrabable ? 1 : 0); // 10
        }

        internal static bool TryParse(string[] objs, int offset, out MessageWorldObject mwo)
        {
            if (objs.Length - offset == 11)
            {
                mwo = new MessageWorldObject();

                mwo.id = int.Parse(objs[offset + 0]);
                mwo.groupId = objs[offset + 1];
                mwo.inventoryId = int.Parse(objs[offset + 2]);
                if (objs[offset + 3].Length > 0)
                {
                    mwo.groupIds = objs[offset + 3].Split(',');
                }
                else
                {
                    mwo.groupIds = new string[0];
                }
                mwo.position = DataTreatments.StringToVector3(objs[offset + 4]);
                mwo.rotation = DataTreatments.StringToQuaternion(objs[offset + 5]);
                mwo.color = MessageHelper.StringToColor(objs[offset + 6]);
                mwo.text = objs[offset + 7];

                mwo.panelIds = new();
                if (objs[offset + 8].Length > 0)
                {
                    var pis = objs[offset + 8].Split(',');
                    for (int i1 = 0; i1 < pis.Length; i1++)
                    {
                        string pi = pis[i1];
                        mwo.panelIds.Add(int.Parse(pi));
                    }
                }

                mwo.growth = float.Parse(objs[offset + 9], CultureInfo.InvariantCulture);
                mwo.makeGrabable = "1" == objs[offset + 10];
                return true;
            } 
            else
            {
                Plugin.LogWarning("Invalid MessageWorldObject: " + objs.Length + " | " + offset);
            }
            mwo = null;
            return false;
        }
    }
}
