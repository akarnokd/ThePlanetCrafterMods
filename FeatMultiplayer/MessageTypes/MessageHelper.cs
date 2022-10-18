using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer.MessageTypes
{
    /// <summary>
    /// Helper methods to parse and assemble messages and their parts.
    /// </summary>
    internal class MessageHelper
    {
        internal static bool TryParseMessage(string type, string str, out string[] parameters)
        {
            if (str.StartsWith(type))
            {
                parameters = str.Split('|');
                return true;
            }
            parameters = null;
            return false;
        }

        /// <summary>
        /// Checks if the <paramref name="str"/> starts with the <paramref name="type"/>,
        /// splits it along the character <c>|</c>, then verifies there are
        /// <paramref name="expectedLength"/> chunks.
        /// </summary>
        /// <param name="type">The expected message type. Include the trailing <c>|</c></param>
        /// <param name="str">The original full message string.</param>
        /// <param name="expectedLength">The number of split chunks expected</param>
        /// <param name="parameters">Output of the split chunks</param>
        /// <returns>True if the message passed the check and was successfully split to chunks</returns>
        internal static bool TryParseMessage(string type, string str, int expectedLength, out string[] parameters)
        {
            if (TryParseMessage(type, str, out parameters))
            {
                if (parameters.Length == expectedLength)
                {
                    return true;
                }
                else
                {
                    Plugin.LogError(type + "Length = " + parameters.Length + ", Expected = " + expectedLength);
                }
            }
            parameters = null;
            return false;
        }

        /// <summary>
        /// Convert an Unity <see cref="Color"/> into a culture-invariant comma separated list
        /// of <c>r,g,b,a</c>
        /// </summary>
        /// <param name="color">The color to convert to string</param>
        /// <returns>The string representation of the <paramref name="color"/></returns>
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

        /// <summary>
        /// Convert a string of <c>r,g,b,a</c> culture-invariantly into an Unity <see cref="Color"/> object.
        /// If the string is null, empty or does not have 4 components. The color <c>0,0,0,0</c> is returned
        /// </summary>
        /// <param name="s">The string to convert</param>
        /// <returns>The color</returns>
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
    }
}
