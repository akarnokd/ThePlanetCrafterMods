using SpaceCraft;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FeatMultiplayer
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

        internal static bool TryParse(string[] objs, int offset, out MessageWorldObject mwo)
        {
            if (objs.Length - offset == 10)
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
                mwo.color = DataTreatments.StringToColor(objs[offset + 6]);
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
                return true;
            }
            mwo = null;
            return false;
        }
    }
}
