// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace LibCommon
{
    /// <summary>
    /// Check if the BepInEx.cfg file has `HideManagerGameObject` is set to true.
    /// </summary>
    internal class BepInExConfigCheck
    {
        internal static readonly string DefaultMessage = "Configuration error.\n\nPlease locate the <color=#FFFF00>BepInEx.cfg</color> file and\nset <color=#FFFF00>HideManagerGameObject = true</color>.\n\nOtherwise, mods won't run correctly.";

        internal static bool Check(Assembly me, ManualLogSource logger)
        {
            string dir = Path.GetDirectoryName(me.Location);

            int i = dir.ToLower(CultureInfo.InvariantCulture).LastIndexOf("bepinex");

            if (i >= 0)
            {
                var newdir = dir[..i] + "bepinex\\config\\BepInEx.cfg";
                
                if (File.Exists(newdir)) 
                {
                    try
                    {
                        var lines = File.ReadAllLines(newdir);

                        foreach (var line in lines)
                        {
                            if (line.StartsWith("HideManagerGameObject"))
                            {
                                if (line.EndsWith("false"))
                                {
                                    logger.LogInfo("HideManagerGameObject = false");
                                    return false;
                                }
                                else if (line.EndsWith("true"))
                                {
                                    logger.LogInfo("HideManagerGameObject = true");
                                    return true;
                                }
                            }
                        }
                    } 
                    catch (Exception ex)
                    {
                        logger.LogInfo(ex);
                    }
                } else
                {
                    logger.LogWarning("Could not locate BepInEx config file: " + newdir);
                }
            }
            else
            {
                logger.LogWarning("Could not locate BepInEx directory: " + dir);
            }
            return false;
        }
    }
}
