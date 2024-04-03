// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using UnityEngine;

namespace FeatSpaceCows
{
    internal class MessageAddRemoveSpaceCow
    {
        internal int parentId;
        internal int inventoryId;
        internal Vector3 position;
        internal Quaternion rotation;
        internal Color color;
        internal bool added;

        internal static bool TryParse(string str, out MessageAddRemoveSpaceCow msg)
        {
            if (str.StartsWith("SpaceCowAddRemove|"))
            {
                var parts = str.Split('|');
                if (parts.Length == 7)
                {
                    msg = new MessageAddRemoveSpaceCow
                    {
                        parentId = int.Parse(parts[1]),
                        inventoryId = int.Parse(parts[2]),
                        position = MessageHelper.StringToVector3(parts[3]),
                        rotation = MessageHelper.StringToQuaternion(parts[4]),
                        color = MessageHelper.StringToColor(parts[5]),
                        added = "1" == parts[6]
                    };
                    return true;
                }
                else
                {
                    Plugin.logger.LogError("Invalid number of arguments to SpaceCowAddRemove: " + parts.Length + ", Expected = 7");
                }
            }
            msg = null;
            return false;
        }

        public override string ToString()
        {
            return "SpaceCowAddRemove|"
                + parentId
                + "|" + inventoryId
                + "|" + MessageHelper.Vector3ToString(position)
                + "|" + MessageHelper.QuaternionToString(rotation)
                + "|" + MessageHelper.ColorToString(color)
                + "|" + (added ? 1 : 0)
            ;
        }
    }
}
