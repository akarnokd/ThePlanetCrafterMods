// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System.Collections.Generic;

namespace LibCommon
{
    /// <summary>
    /// Contains the standard set for ores, butterfly, fish &amp; frogs.
    /// </summary>
    internal class StandardResourceSets
    {
        internal static readonly HashSet<string> defaultOreSet =
        [
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
            "Obsidian",
            "SolarQuartz",
            "MagnetarQuartz",
            "BalzarQuartz", // it is misspelled in the game
            "QuasarQuartz",
            "Dolomite",
            "Uraninite",
            "Bauxite",
            "CosmicQuartz"
        ];

        internal static readonly string defaultOres = string.Join(",", defaultOreSet);

        internal static readonly string defaultLarvae = string.Join(",",
        [
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
        ]);

        internal static readonly string defaultFish = string.Join(",",
        [
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
        ]);

        internal static readonly string defaultFrogs = string.Join(",",
        [
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
            "Frog12Eggs",
            "Frog13Eggs"
        ]);
    }
}
