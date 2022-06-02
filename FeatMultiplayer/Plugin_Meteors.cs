using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin 
    {
        /// <summary>
        /// Called by the SpawnAsteroid patch to ask for a custom landing position
        /// for the given asteroid event.
        /// Receives the current event data, the position of the player and should
        /// return the desired landing position of the asteroid.
        /// </summary>
        public static Func<AsteroidEventData, Vector3, Vector3> asteroidLandingOverride;

        /// <summary>
        /// A component that remembers what the original rocket group was
        /// that started an event.
        /// </summary>
        internal class RocketGroup : MonoBehaviour
        {
            internal string groupId;
        }

        /// <summary>
        /// The vanilla game calls MeteoHandler::SendSomethingInSpace to trigger the meteor event
        /// based on what rocket was sent.
        /// 
        /// On the host, we let it happen.
        /// 
        /// On the client, we prevent it from happening
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="_group"></param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MeteoHandler), nameof(MeteoHandler.SendSomethingInSpace))]
        static bool MeteoHandler_SendSomethingInSpace(MeteoHandler __instance, Group _group)
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        /// <summary>
        /// The vanilla game calls meteoHandler::LaunchSpecificMeteoEvent to
        /// instantiate and queue an AsteroidEventData copy from the incoming MeteoEventData.
        /// 
        /// On the host, we need to figure out what the original rocket group id was,
        /// then attach this information to the newly created AsteroidEventData's gameobject.
        /// 
        /// On the client, we don't do anything.
        /// </summary>
        /// <param name="__instance">The instance to find components on.</param>
        /// <param name="_meteoEvent">What event to start</param>
        /// <param name="___meteoSound">Involves playing a sound.</param>
        /// <param name="___selectedDataMeteoEvent">Stores the current event data.</param>
        /// <param name="___selectedAsteroidEventData">Stores the current asteroid event data</param>
        /// <param name="___asteroidsHandler">Handles the asteroid spawning.</param>
        /// <param name="___timeNewMeteoSet">Save when the meteor event last started.</param>
        /// <returns>False in multiplayer, true in singleplayer</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MeteoHandler), nameof(MeteoHandler.LaunchSpecificMeteoEvent))]
        static bool MeteoHandler_LaunchSpecificMeteoEvent(
            MeteoHandler __instance, 
            MeteoEventData _meteoEvent,
            MeteoSound ___meteoSound,
            ref MeteoEventData ___selectedDataMeteoEvent,
            ref AsteroidEventData ___selectedAsteroidEventData,
            AsteroidsHandler ___asteroidsHandler,
            ref float ___timeNewMeteoSet
        )
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                return false;
            }
            else
            if (updateMode == MultiplayerMode.CoopHost)
            {
                // reverse lookup the meteo event to find out the group id
                var sendInSpace = __instance.GetComponent<MeteoSendInSpace>();
                int evtIndex = sendInSpace.meteoEvents.IndexOf(_meteoEvent);
                string groupId = sendInSpace.groupsData[evtIndex].id;

                ___selectedDataMeteoEvent = _meteoEvent;
                ___meteoSound.StartMeteoAudio(_meteoEvent);
                ___selectedAsteroidEventData = null;
                if (_meteoEvent.asteroidEventData != null)
                {
                    ___selectedAsteroidEventData = UnityEngine.Object.Instantiate<AsteroidEventData>(_meteoEvent.asteroidEventData);
                }
                ___selectedAsteroidEventData.asteroidGameObject.AddComponent<RocketGroup>().groupId = groupId;

                ___asteroidsHandler.AddAsteroidEvent(___selectedAsteroidEventData);
                ___timeNewMeteoSet = Time.time;

                return false;
            }
            return true;
        }

        static Vector3 RandomPointInBounds(Bounds bounds)
        {
            return new Vector3(
                UnityEngine.Random.Range(bounds.min.x, bounds.max.x), 
                UnityEngine.Random.Range(bounds.min.y, bounds.max.y), 
                UnityEngine.Random.Range(bounds.min.z, bounds.max.z));
        }

        /// <summary>
        /// The vanilla game calls AsteroidsHandler::SpawnAsteroid to find a landing
        /// position for an asteriod, part of an asteroid event, and instantiate the
        /// descending object for it.
        /// 
        /// On the host, we have to rewrite it so we can intercept the spawn information
        /// and send it to the client.
        /// 
        /// On the client, we don't do anything.
        /// </summary>
        /// <param name="__instance">The handler instance to to get a rotation transform from.</param>
        /// <param name="_asteroidEvent">The event currently ongoing</param>
        /// <param name="___spawnBoxes">The list of boxes from where asteroids are supposed to start from in the sky.</param>
        /// <param name="___authorizedPlaces">List of acceptable landing positions for the asteroids.</param>
        /// <returns>True in singleplayer, false otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AsteroidsHandler), "SpawnAsteroid")]
        static bool AsteroidsHandler_SpawnAsteroid(
            AsteroidsHandler __instance, 
            AsteroidEventData _asteroidEvent,
            List<Collider> ___spawnBoxes, 
            List<Collider> ___authorizedPlaces)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {

                if (_asteroidEvent.GetExistingAsteroidsCount() >= _asteroidEvent.GetMaxAsteroidsSimultaneous())
                {
                    return false;
                }
                if (_asteroidEvent.GetTotalAsteroidsCount() >= _asteroidEvent.GetMaxAsteroidsTotal())
                {
                    return false;
                }

                Vector3 playerPosition = GetPlayerMainController().gameObject.transform.position;

                Collider collider = ___spawnBoxes[0];
                foreach (Collider collider2 in ___spawnBoxes)
                {
                    if (Vector3.Distance(collider2.transform.position, playerPosition) < Vector3.Distance(collider.transform.position, playerPosition))
                    {
                        collider = collider2;
                    }
                }
                bool landingPositionSet = false;
                Vector3 landingPosition = new Vector3(0, 0, 0);

                // Allow overriding the default randomized landing position
                if (asteroidLandingOverride != null)
                {
                    landingPosition = asteroidLandingOverride.Invoke(_asteroidEvent, playerPosition);
                    landingPositionSet = true;
                }

                // The default logic searches for a random position around the player
                if (!landingPositionSet)
                {
                    int num = 50;
                    for (int i = 0; i < num; i++)
                    {
                        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * (float)_asteroidEvent.distanceFromPlayer;
                        Vector3 candidatePosition = new Vector3(playerPosition.x + randomCircle.x, playerPosition.y, playerPosition.z + randomCircle.y);
                        if (___authorizedPlaces.Any(collider => collider.bounds.Contains(candidatePosition)))
                        {
                            landingPosition = candidatePosition;
                            landingPositionSet = true;
                            break;
                        }
                    }
                }
                if (landingPositionSet)
                {
                    var spawnPoint = RandomPointInBounds(collider.bounds);
                    var asteroidGo = Instantiate<GameObject>(
                        _asteroidEvent.asteroidGameObject,
                        spawnPoint,
                        Quaternion.identity, 
                        __instance.gameObject.transform);

                    asteroidGo.transform.LookAt(landingPosition);
                    asteroidGo.GetComponent<Asteroid>().SetLinkedAsteroidEvent(_asteroidEvent);
                    _asteroidEvent.ChangeExistingAsteroidsCount(1);
                    _asteroidEvent.ChangeTotalAsteroidsCount(1);

                    MessageAsteroidSpawn mas = new()
                    {
                        rocketGroupId = _asteroidEvent.asteroidGameObject.GetComponent<RocketGroup>().groupId,
                        spawnPosition = spawnPoint,
                        landingPosition = landingPosition
                    };

                    Send(mas);
                    Signal();
                }

                return false;
            }
            return updateMode == MultiplayerMode.SinglePlayer;
        }

        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(AsteroidsImpactHandler), "CreateDebris")]
        static void AsteroidImpactHandler_CreateDebris()
        {

        }
        */

        static void ReceiveMessageAsteroidSpawn(MessageAsteroidSpawn mas)
        {
            if (updateMode != MultiplayerMode.CoopClient)
            {
                return;
            }
            var asteroidHandler = Managers.GetManager<AsteroidsHandler>();
            var sendInSpace = Managers.GetManager<MeteoHandler>().GetComponent<MeteoSendInSpace>();

            for (int i = 0; i < sendInSpace.groupsData.Count; i++)
            {
                if (sendInSpace.groupsData[i].id == mas.rocketGroupId)
                {
                    var meteoEvent = sendInSpace.meteoEvents[i];

                    LogInfo("ReceiveMessageAsteroidSpawn: " + mas.rocketGroupId + ", from = " + mas.spawnPosition + ", to = " + mas.landingPosition);

                    var asteroidEventData = UnityEngine.Object.Instantiate<AsteroidEventData>(meteoEvent.asteroidEventData);

                    var asteroidGo = UnityEngine.Object.Instantiate<GameObject>(
                        asteroidEventData.asteroidGameObject, 
                        mas.spawnPosition, 
                        Quaternion.identity,
                        asteroidHandler.gameObject.transform);
                    asteroidGo.transform.LookAt(mas.landingPosition);
                    asteroidGo.GetComponent<Asteroid>().SetLinkedAsteroidEvent(asteroidEventData);

                    return;
                }
            }
            LogInfo("ReceiveMessageAsteroidSpawn:   unable to locate meteoEvent for " + mas.rocketGroupId + ", from = " + mas.spawnPosition + ", to = " + mas.landingPosition);
        }
    }
}
