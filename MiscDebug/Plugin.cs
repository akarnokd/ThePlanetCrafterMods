using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.IO;
using System;
using System.Globalization;
using BepInEx.Logging;

namespace MiscDebug
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscdebug", "(Misc) Debug", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();
        }
    }
}
