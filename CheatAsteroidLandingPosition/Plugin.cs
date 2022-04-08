using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace CheatAsteroidLandingPosition
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatasteroidlandingposition", "(Cheat) Asteroid Landing Position Override", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// Relative position east-west (east is positive).
        /// </summary>
        private static int deltaX;
        /// <summary>
        /// Relative position north-south (north is positive).
        /// </summary>
        private static int deltaZ;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            deltaX = Config.Bind("General", "DeltaX", 100, "Relative position east-west (east is positive).").Value;
            deltaZ = Config.Bind("General", "DeltaY", 0, "Relative position north-south (north is positive).").Value;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AsteroidsHandler), "SpawnAsteroid")]
        static bool AsteroidsHandler_SpawnAsteroid(AsteroidEventData _asteroidEvent, List<Collider> ___authorizedPlaces,
            Collider ___spawnBox, AsteroidsHandler __instance)
        {
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
            Vector3 vector2 = new Vector3(position.x + deltaX, position.y, position.z + deltaZ);
            if (AsteroidsHandler_IsInAuthorizedBounds(vector2, ___authorizedPlaces))
            {
                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(_asteroidEvent.asteroidGameObject,
                    AsteroidsHandler_RandomPointInBounds(___spawnBox.bounds), Quaternion.identity, __instance.gameObject.transform);
                gameObject.transform.LookAt(vector2);
                gameObject.GetComponent<Asteroid>().SetLinkedAsteroidEvent(_asteroidEvent);
                _asteroidEvent.ChangeExistingAsteroidsCount(1);
                _asteroidEvent.ChangeTotalAsteroidsCount(1);
                return false;
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
