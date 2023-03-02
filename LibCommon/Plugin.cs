using BepInEx;
using BepInEx.Logging;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System;
using System.Text;
using System.IO;
using UnityEngine;

namespace LibCommon
{
    [BepInPlugin("akarnokd.theplanetcraftermods.libcommon", "(Lib) Common tools for other mods", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");
            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        private void OnDestroy()
        {
            Logger.LogInfo($"Plugin destroyed!");
        }

    }
}
