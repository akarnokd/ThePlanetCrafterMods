using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeatMultiplayer
{
    internal class MessageTerraformState : MessageStringProvider
    {
        internal float oxygen;
        internal float heat;
        internal float pressure;
        internal float biomass;

        internal static bool TryParse(string str, out MessageTerraformState mts)
        {
            if (MessageHelper.TryParseMessage("TerraformState|", str, out var parameters))
            {
                if (parameters.Length == 5)
                {
                    try
                    {
                        mts = new MessageTerraformState();
                        mts.oxygen = float.Parse(parameters[1], CultureInfo.InvariantCulture);
                        mts.heat = float.Parse(parameters[2], CultureInfo.InvariantCulture);
                        mts.pressure = float.Parse(parameters[3], CultureInfo.InvariantCulture);
                        mts.biomass = float.Parse(parameters[4], CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Plugin.LogError(ex);
                    }
                }
                else
                {
                    Plugin.LogError("TerraformState.Length = " + parameters.Length);
                }
            }
            mts = null;
            return false;
        }

        public string GetString()
        {
            return "TerraformState|" 
                + oxygen.ToString(CultureInfo.InvariantCulture) + "|" 
                + heat.ToString(CultureInfo.InvariantCulture) + "|" 
                + pressure.ToString(CultureInfo.InvariantCulture) + "|"
                + biomass.ToString(CultureInfo.InvariantCulture) + "\n";
        }
    }
}
