// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System.Globalization;
using System.Text;
using UnityEngine;

namespace FeatSpaceCows
{
    internal class MessageHelper
    {
        internal static string ColorToString(Color color)
        {
            StringBuilder sb = new();
            sb.Append(color.r.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(color.g.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(color.b.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(color.a.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static Color StringToColor(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return new Color(0, 0, 0, 0);
            }
            string[] parts = s.Split(',');
            if (parts.Length != 4)
            {
                return new Color(0, 0, 0, 0);
            }
            return new Color(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture)
            );
        }

        internal static string Vector3ToString(Vector3 vec)
        {
            StringBuilder sb = new();
            sb.Append(vec.x.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(vec.y.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(vec.z.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static Vector3 StringToVector3(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return new Vector3(0, 0, 0);
            }
            string[] parts = s.Split(',');
            if (parts.Length != 3)
            {
                return new Vector3(0, 0, 0);
            }
            return new Vector3(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture)
            );
        }

        internal static string QuaternionToString(Quaternion q)
        {
            StringBuilder sb = new();
            sb.Append(q.x.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(q.y.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(q.z.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(q.w.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        internal static Quaternion StringToQuaternion(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return new Quaternion(0, 0, 0, 0);
            }
            string[] parts = s.Split(',');
            if (parts.Length != 4)
            {
                return new Quaternion(0, 0, 0, 0);
            }
            return new Quaternion(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture),
                float.Parse(parts[3], CultureInfo.InvariantCulture)
            );
        }
    }
}
