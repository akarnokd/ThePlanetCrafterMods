using SpaceCraft;
using System;
using System.Globalization;

namespace FeatMultiplayer.MessageTypes
{
    internal class MessageGameMode : MessageBase
    {
        internal DataConfig.GameSettingMode gameMode;

        internal DataConfig.GameSettingDyingConsequences dyingConsequences;

        internal uint worldSeed;

        internal bool unlockedSpaceTrading;

        internal bool unlockedOreExtractors;

        internal bool unlockedTeleporters;

        internal bool unlockedDrones;

        internal bool unlockedAutoCrafter;

        internal bool unlockedEverything;

        internal bool freeCraft;

        internal bool randomizeMineables;

        internal double terraformationPace;

        internal double powerConsumption;

        internal double gaugeDrain;

        internal double meteoOccurrence;


        // Doesn't matter for the client.
        // internal DataConfig.GameSettingStartLocation startLocation;

        internal static bool TryParse(string str, out MessageGameMode mgm)
        {
            if (MessageHelper.TryParseMessage("GameMode|", str, 16, out var parameters))
            {
                try
                {
                    mgm = new();

                    mgm.gameMode = Enum.Parse<DataConfig.GameSettingMode>(parameters[1]);
                    mgm.dyingConsequences = Enum.Parse<DataConfig.GameSettingDyingConsequences>(parameters[2]);
                    mgm.worldSeed = uint.Parse(parameters[3]);
                    mgm.unlockedSpaceTrading = "1" == parameters[4];
                    mgm.unlockedOreExtractors = "1" == parameters[5];
                    mgm.unlockedTeleporters = "1" == parameters[6];
                    mgm.unlockedDrones = "1" == parameters[7];
                    mgm.unlockedAutoCrafter = "1" == parameters[8];
                    mgm.unlockedEverything = "1" == parameters[9];
                    mgm.freeCraft = "1" == parameters[10];
                    mgm.randomizeMineables = "1" == parameters[11];
                    mgm.terraformationPace = double.Parse(parameters[12], CultureInfo.InvariantCulture);
                    mgm.powerConsumption = double.Parse(parameters[13], CultureInfo.InvariantCulture);
                    mgm.gaugeDrain = double.Parse(parameters[14], CultureInfo.InvariantCulture);
                    mgm.meteoOccurrence = double.Parse(parameters[15], CultureInfo.InvariantCulture);

                    return true;
                }
                catch (Exception ex)
                {
                    Plugin.LogError(ex);
                }
            }
            mgm = null;
            return false;
        }

        public override string GetString()
        {
            return "GameMode"
                + "|" + gameMode
                + "|" + dyingConsequences
                + "|" + worldSeed
                + "|" + (unlockedSpaceTrading ? "1" : "0")
                + "|" + (unlockedOreExtractors ? "1" : "0")
                + "|" + (unlockedTeleporters ? "1" : "0")
                + "|" + (unlockedDrones ? "1" : "0")
                + "|" + (unlockedAutoCrafter ? "1" : "0")
                + "|" + (unlockedEverything ? "1" : "0")
                + "|" + (freeCraft ? "1" : "0")
                + "|" + (randomizeMineables ? "1" : "0")
                + "|" + terraformationPace.ToString(CultureInfo.InvariantCulture)
                + "|" + powerConsumption.ToString(CultureInfo.InvariantCulture)
                + "|" + gaugeDrain.ToString(CultureInfo.InvariantCulture)
                + "|" + meteoOccurrence.ToString(CultureInfo.InvariantCulture)
                + "\n";
        }
    }
}
