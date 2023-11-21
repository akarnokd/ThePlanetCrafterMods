using System;
using System.Collections.Generic;
using System.Text;

namespace LibCommon
{
    /// <summary>
    /// Contains the standard set for ores, butterfly, fish &amp; frogs.
    /// </summary>
    internal class StandardResourceSets
    {
        internal static readonly string defaultOres = string.Join(",", new string[]
        {
            "Cobalt",
            "Silicon",
            "Iron",
            "ice", // it is not capitalized in the game
            "Magnesium",
            "Titanium",
            "Aluminium",
            "Uranim", // it is misspelled in the game
            "Iridium",
            "Alloy",
            "Zeolite",
            "Osmium",
            "Sulfur",
            "PulsarQuartz",
            "PulsarShard",
            "Obsidian"
        });

        internal static readonly string defaultLarvae = string.Join(",", new string[]
        {
            "LarvaeBase1",
            "LarvaeBase2",
            "LarvaeBase3",
            "Butterfly11Larvae",
            "Butterfly12Larvae",
            "Butterfly13Larvae",
            "Butterfly14Larvae",
            "Butterfly15Larvae",
            "Butterfly16Larvae",
            "Butterfly17Larvae",
            "Butterfly18Larvae",
            "Butterfly19Larvae"
        });

        internal static readonly string defaultFish = string.Join(",", new string[]
        {
            "Fish1Eggs",
            "Fish2Eggs",
            "Fish3Eggs",
            "Fish4Eggs",
            "Fish5Eggs",
            "Fish6Eggs",
            "Fish7Eggs",
            "Fish8Eggs",
            "Fish9Eggs",
            "Fish10Eggs",
            "Fish11Eggs",
            "Fish12Eggs",
            "Fish13Eggs",
        });

        internal static readonly string defaultFrogs = string.Join(",", new string[]
        {
            "Frog1Eggs",
            "Frog2Eggs",
            "Frog3Eggs",
            "Frog4Eggs",
            "Frog5Eggs",
            "Frog6Eggs",
            "Frog7Eggs",
            "Frog8Eggs",
            "Frog9Eggs",
            "Frog10Eggs",
            "FrogGoldEggs",
            "Frog11Eggs",
        });
    }
}
