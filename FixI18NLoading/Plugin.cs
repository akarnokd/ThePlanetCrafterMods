using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace FixI18NLoading
{
    [BepInPlugin("akarnokd.theplanetcraftermods.fixi18nloading", "(Fix) International Loading", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DataTreatments), nameof(DataTreatments.StringToColor))]
        static bool DataTreatments_StringToColor(string _colorString, ref Color __result, char ___colorDelimiter)
        {
            if (string.IsNullOrEmpty(_colorString))
            {
                __result = new Color(0f, 0f, 0f, 0f);
                return false;
            }
            string[] components = _colorString.Split(new char[] { ___colorDelimiter });
            if (components.Length != 4)
            {
                __result = new Color(0f, 0f, 0f, 0f);
                return false;
            }
            __result = new Color(
                float.Parse(components[0].Replace(',', '.'), CultureInfo.InvariantCulture),
                float.Parse(components[1].Replace(',', '.'), CultureInfo.InvariantCulture),
                float.Parse(components[2].Replace(',', '.'), CultureInfo.InvariantCulture),
                float.Parse(components[3].Replace(',', '.'), CultureInfo.InvariantCulture)
            );
            return false;
        }
    }
}
