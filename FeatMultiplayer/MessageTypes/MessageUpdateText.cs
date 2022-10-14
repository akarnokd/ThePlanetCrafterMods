using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageUpdateText : MessageBase
    {
        internal int id;
        internal string text;

        internal static bool TryParse(string str, out MessageUpdateText mut)
        {
            if (MessageHelper.TryParseMessage("UpdateText|", str, out var parameters))
            {
                if (parameters.Length == 3)
                {
                    try
                    {
                        mut = new MessageUpdateText();
                        mut.id = int.Parse(parameters[1]);
                        mut.text = parameters[2];
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError(ex);
                    }
                }
                else
                {
                    Plugin.LogError("MessageUpdateText.Length = " + parameters.Length);
                }
            }
            mut = null;
            return false;
        }

        public override string GetString()
        {
            return "UpdateText|" + id + "|" + text + "\n";
        }
    }
}
