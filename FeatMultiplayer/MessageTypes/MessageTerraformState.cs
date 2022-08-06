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
        internal float plants;
        internal float insects;
        internal float animals;

        internal static bool TryParse(string str, out MessageTerraformState mts)
        {
            if (MessageHelper.TryParseMessage("TerraformState|", str, 7, out var parameters))
            {
                try
                {
                    mts = new MessageTerraformState();
                    mts.oxygen = float.Parse(parameters[1], CultureInfo.InvariantCulture);
                    mts.heat = float.Parse(parameters[2], CultureInfo.InvariantCulture);
                    mts.pressure = float.Parse(parameters[3], CultureInfo.InvariantCulture);
                    mts.plants = float.Parse(parameters[4], CultureInfo.InvariantCulture);
                    mts.insects = float.Parse(parameters[5], CultureInfo.InvariantCulture);
                    mts.animals = float.Parse(parameters[6], CultureInfo.InvariantCulture);
                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mts = null;
            return false;
        }

        public string GetString()
        {
            return "TerraformState|" 
                + oxygen.ToString(CultureInfo.InvariantCulture)
                + "|" + heat.ToString(CultureInfo.InvariantCulture)
                + "|" + pressure.ToString(CultureInfo.InvariantCulture)
                + "|" + plants.ToString(CultureInfo.InvariantCulture)
                + "|" + insects.ToString(CultureInfo.InvariantCulture)
                + "|" + animals.ToString(CultureInfo.InvariantCulture)
                + "\n";
        }

        /// <summary>
        /// Returns the Terraformation Index derived from the components.
        /// </summary>
        /// <returns>the Terraformation Index derived from the components.</returns>
        public float GetTi()
        {
            return oxygen + heat + pressure + plants + insects + animals;
        }

        public float GetBiomass()
        {
            return plants + insects + animals;
        }
    }
}
