using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageConsume : MessageBase
    {
        internal int worldObjectId;
        internal int inventoryId;

        internal static bool TryParse(string str, out MessageConsume mmu)
        {
            if (MessageHelper.TryParseMessage("Consume|", str, 3, out var parameters))
            {
                mmu = new MessageConsume();
                mmu.worldObjectId = int.Parse(parameters[1]);
                mmu.inventoryId = int.Parse(parameters[2]);
                return true;
            }
            mmu = null;
            return false;
        }

        public override string GetString()
        {
            return "Consume|" + worldObjectId + "|" + inventoryId + "\n";
        }
    }
}
