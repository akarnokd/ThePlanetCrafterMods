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
    internal class MessageConstructs
    {
        internal List<MessageWorldObject> worldObjects = new();
        internal static void AppendWorldObject(StringBuilder sb, WorldObject wo)
        {
            sb.Append(wo.GetId());  // 0
            sb.Append(';');
            sb.Append(wo.GetGroup().GetId());  // 1
            sb.Append(';');
            sb.Append(wo.GetLinkedInventoryId());  // 2
            sb.Append(';');
            sb.Append(GroupsHandler.GetGroupsStringIds(wo.GetLinkedGroups())); // 3
            sb.Append(';');
            if (wo.GetIsPlaced())
            {
                sb.Append(DataTreatments.Vector3ToString(wo.GetPosition())); // 4
                sb.Append(';');
                sb.Append(DataTreatments.QuaternionToString(wo.GetRotation())); // 5
                sb.Append(';');
            }
            else
            {
                sb.Append(";;");
            }
            sb.Append(DataTreatments.ColorToString(wo.GetColor())); // 6
            sb.Append(';');
            sb.Append(wo.GetText()); // 7
            sb.Append(';');
            sb.Append(DataTreatments.IntListToString(wo.GetPanelsId())); // 8
            sb.Append(';');
            sb.Append(wo.GetGrowth().ToString(CultureInfo.InvariantCulture)); // 9
        }
        public static bool TryParse(string str, out MessageConstructs mc)
        {
            if (MessageHelper.TryParseMessage("Constructs|", str, out var parameters))
            {
                try
                {
                    mc = new MessageConstructs();

                    for (int i = 1; i < parameters.Length; i++)
                    {
                        if (parameters[i].Length == 0)
                        {
                            continue;
                        }
                        string[] objs = parameters[i].Split(';');
                        if (objs.Length == 9)
                        {
                            MessageWorldObject mwo = new MessageWorldObject();

                            mwo.id = int.Parse(objs[0]);
                            mwo.groupId = objs[1];
                            mwo.inventoryId = int.Parse(objs[2]);
                            if (objs[3].Length > 0)
                            {
                                mwo.groupIds = objs[3].Split(',');
                            }
                            else
                            {
                                mwo.groupIds = new string[0];
                            }
                            mwo.position = DataTreatments.StringToVector3(objs[4]);
                            mwo.rotation = DataTreatments.StringToQuaternion(objs[5]);
                            mwo.color = DataTreatments.StringToColor(objs[6]);
                            mwo.text = objs[7];

                            if (objs[8].Length > 0)
                            {
                                var pis = objs[8].Split(',');
                                mwo.panelIds = new int[pis.Length];
                                for (int i1 = 0; i1 < pis.Length; i1++)
                                {
                                    string pi = pis[i1];
                                    mwo.panelIds[i1] = int.Parse(pi);
                                }
                            }
                            else
                            {
                                mwo.panelIds = new int[0];
                            }

                            mwo.growth = float.Parse(objs[9], CultureInfo.InvariantCulture);

                            mc.worldObjects.Add(mwo);
                        }
                    }

                    return true;
                } 
                catch (Exception)
                {

                }
            }
            mc = null;
            return false;
        }
    }

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
        public int[] panelIds;
        public float growth;
    }
}
