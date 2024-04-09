// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

namespace CheatAsteroidLandingPosition
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatasteroidlandingposition", "(Cheat) Asteroid Landing Position Override", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// Relative position up-down.
        /// </summary>
        static ConfigEntry<int> deltaY;
        /// <summary>
        /// Relative position east-west (east is positive).
        /// </summary>
        static ConfigEntry<int> deltaX;
        /// <summary>
        /// Relative position north-south (north is positive).
        /// </summary>
        static ConfigEntry<int> deltaZ;

        /// <summary>
        /// Should the coordinates treated as absolute?
        /// </summary>
        static ConfigEntry<bool> absolute;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            deltaX = Config.Bind("General", "DeltaX", 100, "Relative position east-west (east is positive).");
            deltaY = Config.Bind("General", "DeltaY", 0, "Relative position up-down.");
            deltaZ = Config.Bind("General", "DeltaZ", 0, "Relative position north-south (north is positive).");
            absolute = Config.Bind("General", "Absolute", false, "Should the DeltaX, DeltaY and DeltaZ interpreted instead of absolute coordinates?.");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AsteroidsHandler), "SpawnAsteroid")]
        static bool AsteroidsHandler_SpawnAsteroid(
            AsteroidsHandler __instance,
            AsteroidEventData asteroidEvent, 
            List<Collider> ___authorizedPlaces,
            List<Collider> ___spawnBoxes,
            ref Unity.Mathematics.Random ____random)
        {
            // Unfortunately, I have to copy out the original sources and patch inbetween some instructions

            if (asteroidEvent.GetExistingAsteroidsCount() >= asteroidEvent.GetMaxAsteroidsSimultaneous())
            {
                return false;
            }
            if (asteroidEvent.GetTotalAsteroidsCount() >= asteroidEvent.GetMaxAsteroidsTotal())
            {
                return false;
            }

            Vector3 playerPosition = Vector3.zero;

            var pm = Managers.GetManager<PlayersManager>();

            foreach (var pc in pm.playersControllers)
            {
                if (pc.IsHost)
                {
                    playerPosition = pc.transform.position;
                }
            }
            // just in case playersControllers is empty in singleplayer.
            if (playerPosition == Vector3.zero)
            {
                playerPosition = pm.GetActivePlayerController().transform.position;
            }

            Collider collider = ___spawnBoxes[0];
            foreach (Collider collider2 in ___spawnBoxes)
            {
                if (Vector3.Distance(collider2.transform.position, playerPosition) < Vector3.Distance(collider.transform.position, playerPosition))
                {
                    collider = collider2;
                }
            }
            // Vector2 vector = UnityEngine.Random.insideUnitCircle * (float)_asteroidEvent.distanceFromPlayer;
            var landingPosition = new Vector3(deltaX.Value, deltaY.Value, deltaZ.Value);
            if (!absolute.Value)
            {
                landingPosition += playerPosition;
            }
            if (AsteroidsHandler_IsInAuthorizedBounds(landingPosition, ___authorizedPlaces))
            {
                GameObject gameObject = Instantiate(asteroidEvent.asteroidGameObject, AsteroidsHandler_RandomPointInBounds(collider.bounds), Quaternion.identity, __instance.gameObject.transform);
                gameObject.transform.LookAt(landingPosition);
                var asteroid = gameObject.GetComponent<Asteroid>();
                gameObject.GetComponent<Asteroid>().DefineVariables(____random, NetworkManager.Singleton != null && (__instance.ReferenceClientId == NetworkManager.Singleton.LocalClientId || (NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.ConnectedClients.ContainsKey(__instance.ReferenceClientId))));
                asteroid.SetLinkedAsteroidEvent(asteroidEvent);
                asteroidEvent.ChangeExistingAsteroidsCount(1);
                asteroidEvent.ChangeTotalAsteroidsCount(1);
            }
            return false;
        }

        static bool AsteroidsHandler_IsInAuthorizedBounds(Vector2 _position, List<Collider> authorizedPlaces)
        {
            using List<Collider>.Enumerator enumerator = authorizedPlaces.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.bounds.Contains(_position))
                {
                    return true;
                }
            }
            return false;
        }

        static Vector3 AsteroidsHandler_RandomPointInBounds(Bounds bounds)
        {
            return new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x), 
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y), 
                UnityEngine.Random.Range(bounds.min.z, bounds.max.z)
            );
        }
    }
}
