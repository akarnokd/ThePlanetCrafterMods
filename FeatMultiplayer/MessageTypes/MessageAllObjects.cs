using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageAllObjects : MessageBase
    {
        internal List<MessageWorldObject> worldObjects = new();
        
        public static bool TryParse(string str, out MessageAllObjects mc)
        {
            if (MessageHelper.TryParseMessage("AllObjects|", str, out var parameters))
            {
                try
                {
                    mc = new MessageAllObjects();

                    for (int i = 1; i < parameters.Length; i++)
                    {
                        if (parameters[i].Length == 0)
                        {
                            continue;
                        }
                        string[] objs = parameters[i].Split(';');
                        if (MessageWorldObject.TryParse(objs, 0, out var mwo))
                        {
                            mc.worldObjects.Add(mwo);
                        }
                    }

                    return true;
                } 
                catch (Exception ex)
                {
                    Plugin.LogError(str +"\r\n\r\n" + ex);
                }
            }
            mc = null;
            return false;
        }

        public override string GetString()
        {
            throw new NotImplementedException();
        }
    }
}
