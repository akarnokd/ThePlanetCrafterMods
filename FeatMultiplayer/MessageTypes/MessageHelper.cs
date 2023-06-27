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

        /// <summary>
        /// Encode world object text by replacing semicolos, pipes, backslashes and newlines with escape sequences.
        /// </summary>
        /// <param name="text">the text to encode</param>
        /// <param name="sb">the output for the encoded text</param>
        internal static void EncodeText(string text, StringBuilder sb)
        {
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == ';')
                    {
                        sb.Append("\\s");
                    }
                    else if (c == '|')
                    {
                        sb.Append("\\p");
                    }
                    else if (c == '\\')
                    {
                        sb.Append("\\b");
                    }
                    else if (c == '\n')
                    {
                        sb.Append("\\n");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
            }
        }

        /// <summary>
        /// Decode world object text from its message-encoded format by replacing \s with semicolon, \p with pipe,
        /// \b with backslash and \n with newline.
        /// </summary>
        /// <param name="text">The text to decode.</param>
        /// <returns>The decoded text.</returns>
        internal static string DecodeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            StringBuilder sb = new(text.Length + 1);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    c = text[i + 1];
                    if (c == 's')
                    {
                        sb.Append(';');
                    }
                    else if (c == 'p')
                    {
                        sb.Append('|');
                    }
                    else if (c == 'b')
                    {
                        sb.Append('\\');
                    }
                    else if (c == 'n')
                    {
                        sb.Append('\n');
                    }
                    i++;
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Converts a vector into a string x,y,z where each number has at most
        /// 2 decimal places.
        /// </summary>
        /// <param name="vector">The vector to convert</param>
        /// <returns>The string representation</returns>
        internal static string Vector3ToStringReducedPrecision(Vector3 vector)
        {
            FormattableString fs = $"{vector.x:0.##},{vector.y:0.##},{vector.z:0.##}";
            return fs.ToString(CultureInfo.InvariantCulture);
        }
    }
}
