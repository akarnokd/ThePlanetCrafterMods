using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageCustom : MessageBase
    {
        internal object o;

        public override string GetString()
        {
            return o.ToString();
        }
    }
}
