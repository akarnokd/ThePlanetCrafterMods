using BepInEx;
using UnityEngine;
using BepInEx.Bootstrap;
using System.Reflection;
using System;
using HarmonyLib;
using SpaceCraft;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Text;

namespace ZipRest
{
    [BepInPlugin("akarnokd.theplanetcraftermods.ziprest", "Zip the other mods", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            // Harmony.CreateAndPatchAll(typeof(Plugin));
        }
    }
}
