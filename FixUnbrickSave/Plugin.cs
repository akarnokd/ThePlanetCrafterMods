using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;

namespace FixUnbrickSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixunbricksave", "(Fix) Unbrick Save", "1.0.0.3")]
    public class Plugin : BaseUnityPlugin
    {

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            //Harmony.CreateAndPatchAll(typeof(Plugin));
        }
    }
}
