// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using UnityEngine;

namespace LibCommon
{
    /// <summary>
    /// Provides a callback for when the game has finished loading the given
    /// planet and all related procedural instances.
    /// Use the <see cref="Patch(Harmony, Action{PlanetData})"/> to setup a callback.
    /// </summary>
    /// <remarks>
    /// Had to introduce a common lib for this because the planet loading
    /// has changed a couple times in considerable ways and I don't
    /// want to update all mods depending on this mechanism individually.
    /// </remarks>
    public class ModPlanetLoaded
    {
        static Action<PlanetLoader> _onPlanetLoaded;

        static string _debugInfo;

        /// <summary>
        /// Uses the given harmony instance to patch the PlanetLoader::HandleDataAfterLoad
        /// method to wait for all clear and call the given callback.
        /// </summary>
        /// <param name="harmony"></param>
        /// <param name="onPlanetLoaded"></param>
        public static void Patch(Harmony harmony, string debugInfo, Action<PlanetLoader> onPlanetLoaded)
        {
            _onPlanetLoaded = onPlanetLoaded ?? throw new ArgumentNullException(nameof(onPlanetLoaded));
            _debugInfo = debugInfo;

            harmony.PatchAll(typeof(ModPlanetLoaded));
        }

        /// <summary>
        /// The vanilla starts a coroutine that waits for the procgen instances
        /// to be ready. For now, we start a companion coroutine which waits for
        /// the <see cref="PlanetLoader.GetIsLoaded"/> to turn true.
        /// </summary>
        /// <param name="__instance">The PlanetLoader instance to latch onto.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        static void Patch_PlanetLoader_HandleDataAfterLoad(PlanetLoader __instance)
        {
            UnityLogWriter.WriteStringToUnityLog("ModPlanetLoaded (1): " + _debugInfo + "\n");

            __instance.StartCoroutine(WaitForProceduralInstances(__instance));
        }

        static IEnumerator WaitForProceduralInstances(PlanetLoader __instance)
        {
            while (
                /*
                ProceduralInstancesHandler.Instance == null 
                || !ProceduralInstancesHandler.Instance.IsReady
                || */
                !__instance.GetIsLoaded())
            {
                yield return null;
            }
            while ((Managers.GetManager<PlayersManager>()?.GetActivePlayerController() ?? null) == null)
            {
                yield return null;
            }

            try
            {
                _onPlanetLoaded?.Invoke(__instance);
            }
            catch (Exception e)
            {
                UnityLogWriter.WriteStringToUnityLog("ModPlanetLoaded (1.5): " + _debugInfo + "\n" + e + "\n");

            }

            UnityLogWriter.WriteStringToUnityLog("ModPlanetLoaded (2): " + _debugInfo + "\n");
        }
    }
}
