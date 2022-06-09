using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageCustom : MessageStringProvider
    {
        internal object o;

        public string GetString()
        {
            return o.ToString();
        }
    }
}
