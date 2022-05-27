using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageMined
    {
        internal int id;

        internal static bool TryParse(string str, out MessageMined mm)
        {
            if (MessageHelper.TryParseMessage("Mined|", str, out var parameters))
            {
                try
                {
                    if (parameters.Length == 2)
                    {
                        mm = new MessageMined();
                        mm.id = int.Parse(parameters[1]);
                        return true;
                    }
                }
                catch (Exception)
                {

                }
            }
            mm = null;
            return false;
        }
    }
}
