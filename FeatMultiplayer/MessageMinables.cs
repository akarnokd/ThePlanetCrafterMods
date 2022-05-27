using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageMinables
    {
        internal int[] ids;

        internal static bool TryParse(string str, out MessageMinables mm)
        {
            if (MessageHelper.TryParseMessage("Minables|", str, out var parameters))
            {
                try
                {
                    mm = new MessageMinables();
                    mm.ids = new int[parameters.Length - 1];
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        mm.ids[i - 1] = int.Parse(parameters[i]);
                    }

                    return true;
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
