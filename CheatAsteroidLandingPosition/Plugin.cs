using BepInEx;
using BepInEx.Configuration;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using System;

namespace CheatAsteroidLandingPosition
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatasteroidlandingposition", "(Cheat) Asteroid Landing Position Override", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        /// <summary>
        /// Relative position east-west (east is positive).
        /// </summary>
        static ConfigEntry<int> deltaX;
        /// <summary>
        /// Relative position north-south (north is positive).
        /// </summary>
        static ConfigEntry<int> deltaZ;

        static MethodInfo multiplayerCurrentMode;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            deltaX = Config.Bind("General", "DeltaX", 100, "Relative position east-west (east is positive).");
            deltaZ = Config.Bind("General", "DeltaZ", 0, "Relative position north-south (north is positive).");

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                multiplayerCurrentMode = AccessTools.Method(pi.Instance.GetType(), "GetMultiplayerMode");

                Func<AsteroidEventData, Vector3, Vector3> positionOverride = (ae, position) => 
                    new Vector3(position.x + /* vector.x + */ deltaX.Value, position.y, position.z /* + vector.y */ + deltaZ.Value);

                AccessTools.Field(pi.Instance.GetType(), "asteroidLandingOverride").SetValue(null, positionOverride);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AsteroidsHandler), "SpawnAsteroid")]
        static bool AsteroidsHandler_SpawnAsteroid(AsteroidEventData _asteroidEvent, 
            List<Collider> ___authorizedPlaces,
            List<Collider> ___spawnBoxes, 
            AsteroidsHandler __instance)
        {
            // in multiplayer mode, simply do nothing
            if (multiplayerCurrentMode != null && ((string)multiplayerCurrentMode.Invoke(null, new object[0]) != "SinglePlayer"))
            {
                return false;
            }

            // Unfortunately, I have to copy out the original sources and patch inbetween some instructions

            if (_asteroidEvent.GetExistingAsteroidsCount() >= _asteroidEvent.GetMaxAsteroidsSimultaneous())
            {
                return false;
            }
            if (_asteroidEvent.GetTotalAsteroidsCount() >= _asteroidEvent.GetMaxAsteroidsTotal())
            {
                return false;
            }
            Vector3 position = Managers.GetManager<PlayersManager>().GetActivePlayerController().gameObject.transform.position;
            Collider collider = ___spawnBoxes[0];
            foreach (Collider collider2 in ___spawnBoxes)
            {
                if (Vector3.Distance(collider2.transform.position, position) < Vector3.Distance(collider.transform.position, position))
                {
                    collider = collider2;
                }
            }
            // Vector2 vector = UnityEngine.Random.insideUnitCircle * (float)_asteroidEvent.distanceFromPlayer;
            Vector3 vector2 = new Vector3(position.x + /* vector.x + */ deltaX.Value, position.y, position.z /* + vector.y */ + deltaZ.Value);
            if (AsteroidsHandler_IsInAuthorizedBounds(vector2, ___authorizedPlaces))
            {
                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(_asteroidEvent.asteroidGameObject, AsteroidsHandler_RandomPointInBounds(collider.bounds), Quaternion.identity, __instance.gameObject.transform);
                gameObject.transform.LookAt(vector2);
                gameObject.GetComponent<Asteroid>().SetLinkedAsteroidEvent(_asteroidEvent);
                _asteroidEvent.ChangeExistingAsteroidsCount(1);
                _asteroidEvent.ChangeTotalAsteroidsCount(1);
            }
            return false;
        }

        static bool AsteroidsHandler_IsInAuthorizedBounds(Vector2 _position, List<Collider> authorizedPlaces)
        {
            using (List<Collider>.Enumerator enumerator = authorizedPlaces.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.bounds.Contains(_position))
                    {
                        return true;
                    }
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
