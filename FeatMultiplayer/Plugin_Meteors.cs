using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections;
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

        static float debrisUpdateDelay = 0.05f;

        /// <summary>
        /// Tracks the location of the debris resource object, updates
        /// its associated world object and the client about its position.
        /// </summary>
        internal class DebrisResourceTracker : MonoBehaviour
        {
            internal float timeToLive;
            internal WorldObject worldObject;

            internal void StartTracking()
            {
                StartCoroutine(Tracker());
            }

            IEnumerator Tracker()
            {
                float until = Time.time + timeToLive;
                for (; ; )
                {
                    if (until > Time.time)
                    {
                        if (worldObject.GetIsPlaced())
                        {
                            worldObject.SetPositionAndRotation(gameObject.transform.position, gameObject.transform.rotation);
                            Send(new MessageSetTransform()
                            {
                                id = worldObject.GetId(),
                                mode = MessageSetTransform.Mode.Both,
                                position = worldObject.GetPosition(),
                                rotation = worldObject.GetRotation()
                            });
                            Signal();
                        }
                        yield return new WaitForSeconds(debrisUpdateDelay);
                    }
                    else
                    {
                        break;
                    }
                }
                if (worldObject.GetIsPlaced())
                {
                    WorldObjectsHandler.DestroyWorldObject(worldObject);
                }
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Destroys the associated debris game object, then re-enables
        /// the rigid body on the other known game objects, so they can
        /// fall down from the sky.
        /// 
        /// Not strictly necessary for multiplayer, but wanted to do it
        /// anyway as it looks awkward when they pile up in the air.
        /// </summary>
        internal class DebrisDespawnManager : MonoBehaviour
        {
            internal List<GameObject> reenableRigidBody;

            internal void BeginWait(float ttl)
            {
                StartCoroutine(WaitTracker(ttl));
            }
            IEnumerator WaitTracker(float ttl)
            {
                yield return new WaitForSeconds(ttl);
                Destroy(gameObject);

                for (int i = reenableRigidBody.Count - 1; i >= 0; i--)
                {
                    GameObject go = reenableRigidBody[i];
                    if (go != null)
                    {
                        Rigidbody rigidbody = go.GetComponent<Rigidbody>();
                        if (rigidbody == null)
                        {
                            rigidbody = go.AddComponent<Rigidbody>();
                            rigidbody.mass = 200f;
                            rigidbody.drag = 0.5f;
                        }

                        // add 20 seconds more to the destruction of the rigid body
                        Destroy(go.GetComponent<ComponentDestroyAfterDelay>());
                        go.AddComponent<ComponentDestroyAfterDelay>().DestroyAfterDelay(20f, rigidbody);
                    }
                    else
                    {
                        reenableRigidBody.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// The game uses AsteroidImpactHandler::CreateDebris to create the rocks and
        /// resources after the impact. It also it limits the total number of rocks
        /// displayed.
        /// 
        /// On the host, we have to rewrite it to intercept the creation of the resources
        /// and sync it to the client as basic resources with no physics, just tracking the
        /// position changes from the server. Similar to how dropped items get tracked.
        /// 
        /// On the client, we don't spawn the resources, only the debris and rely on the
        /// position tracking of host-created resources via their world objects.
        /// </summary>
        /// <param name="_impactPosition">Where the asteroid impacted.</param>
        /// <param name="_container">The parent game object to store the created objects</param>
        /// <param name="_asteroid">the configuration of the asteroid</param>
        /// <param name="___ignoredLayerMasks">What layers to ignore when raycasting for the actual point on the ground where the resource is thrown up.</param>
        /// <param name="___debrisPool">Pool of currently visible rocks.</param>
        /// <param name="___maxTotalDebris">How many rocks should be displayed.</param>
        /// <param name="___spawnedResourcesDestroyMultiplier">How long the resource object should live.</param>
        /// <returns>False in multiplayer, true in singleplayer.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(AsteroidsImpactHandler), "CreateDebris")]
        static bool AsteroidImpactHandler_CreateDebris(
            Vector3 _impactPosition, GameObject _container, Asteroid _asteroid,
            LayerMask ___ignoredLayerMasks,
            List<GameObject> ___debrisPool,
            int ___maxTotalDebris,
            int ___spawnedResourcesDestroyMultiplier
        )
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                List<GameObject> reenableRigidBody = new List<GameObject>();

                var ip = _impactPosition;
                float num = 0.1f;
                float num2 = 0.1f;
                float num3 = 0.025f;
                int num4 = 5;
                float num5 = 1f;
                for (int i = 0; i < _asteroid.GetDebrisNumber(); i++)
                {
                    float d = _asteroid.GetDebrisSize() - num3 * (float)i;
                    float num6 = -1f;
                    Vector2 vector = UnityEngine.Random.insideUnitCircle * num;
                    Vector3 vector2 = new Vector3(ip.x + vector.x, ip.y, ip.z + vector.y);
                    Vector3 origin = new Vector3(vector2.x, vector2.y + 15f, vector2.z);
                    Vector3 direction = Vector3.up * -1f;
                    RaycastHit raycastHit;
                    if (Physics.Raycast(new Ray(origin, direction), out raycastHit, 5000f, ___ignoredLayerMasks))
                    {
                        float numberOfResourceInDebris = _asteroid.GetNumberOfResourceInDebris();
                        bool flag = _asteroid.GetAssociatedGroups() != null && _asteroid.GetAssociatedGroups().Count > 0 && i > num4;
                        GameObject debrisGo = null;
                        if ((float)(i - num4) <= numberOfResourceInDebris && flag)
                        {
                            d = 1f;

                            // Create resources only on the host
                            if (updateMode == MultiplayerMode.CoopHost)
                            {
                                var groupItem = _asteroid.GetAssociatedGroups()[UnityEngine.Random.Range(0, _asteroid.GetAssociatedGroups().Count)];
                                GameObject resourceTemplateGo = groupItem.GetAssociatedGameObject();
                                debrisGo = UnityEngine.Object.Instantiate<GameObject>(resourceTemplateGo);
                                CapsuleCollider componentInChildren = debrisGo.GetComponentInChildren<CapsuleCollider>();
                                if (componentInChildren != null)
                                {
                                    componentInChildren.radius *= 0.75f;
                                }

                                // Create the world object for it now
                                var wo = WorldObjectsHandler.CreateNewWorldObject(groupItem);
                                // debris is not saveable by default
                                // wo.SetDontSaveMe(true);

                                WorldObjectAssociated woa = debrisGo.GetComponent<WorldObjectAssociated>();
                                if (woa == null)
                                {
                                    woa = debrisGo.AddComponent<WorldObjectAssociated>();
                                }

                                woa.SetWorldObject(wo);
                                gameObjectByWorldObject[wo] = debrisGo;

                                SendWorldObject(wo, false);

                                // setup the position tracker
                                var dt = debrisGo.AddComponent<DebrisResourceTracker>();
                                dt.timeToLive = _asteroid.GetDebrisDestroyTime() * (float)___spawnedResourcesDestroyMultiplier;
                                dt.worldObject = wo;
                                dt.StartTracking();
                            }
                        }
                        else
                        {
                            GameObject gameObject = _asteroid.GetDebrisObjects()[UnityEngine.Random.Range(0, _asteroid.GetDebrisObjects().Count)];
                            debrisGo = CreateOrGetDebris(gameObject, ___debrisPool, ___maxTotalDebris);
                            float ttl = UnityEngine.Random.Range(_asteroid.GetDebrisDestroyTime() / 2f, _asteroid.GetDebrisDestroyTime() * 2f);

                            var ddm = debrisGo.AddComponent<DebrisDespawnManager>();
                            ddm.reenableRigidBody = reenableRigidBody;
                            ddm.BeginWait(ttl);
                        }

                        if (debrisGo != null)
                        {
                            reenableRigidBody.Add(debrisGo);

                            debrisGo.transform.parent = _container.transform;
                            debrisGo.transform.position = raycastHit.point + Vector3.up * 0.2f;
                            Rigidbody rigidbody = debrisGo.GetComponent<Rigidbody>();
                            if (rigidbody == null)
                            {
                                rigidbody = debrisGo.AddComponent<Rigidbody>();
                            }
                            rigidbody.mass = 200f;
                            if (i < num4)
                            {
                                d = _asteroid.GetDebrisSize() * 2f;
                                num6 = 1f;
                                rigidbody.drag = 1f;
                                debrisGo.transform.position = vector2;
                            }
                            else
                            {
                                rigidbody.drag = 0.5f;
                                rigidbody.AddForce(raycastHit.normal * 1500f, ForceMode.Impulse);
                            }
                            // destroy the rigid body after some time
                            var destroyDelay = debrisGo.GetComponent<ComponentDestroyAfterDelay>();
                            if (destroyDelay == null)
                            {
                                destroyDelay = debrisGo.AddComponent<ComponentDestroyAfterDelay>();
                            }
                            destroyDelay.DestroyAfterDelay(20f, rigidbody);

                            debrisGo.transform.position = new Vector3(debrisGo.transform.position.x, debrisGo.transform.position.y - num6, debrisGo.transform.position.z);
                            float z = UnityEngine.Random.value * 360f;
                            Quaternion lhs = Quaternion.Euler(0f, 0f, z);
                            Quaternion rotation = Quaternion.LookRotation(raycastHit.normal) * (lhs * Quaternion.Euler(90f, 0f, 0f));
                            debrisGo.transform.rotation = rotation;
                            debrisGo.transform.localScale *= d;
                            if (debrisGo.transform.localScale.x < num5)
                            {
                                debrisGo.transform.localScale = new Vector3(num5, num5, num5);
                            }
                        }
                    }
                    num += num2;
                }
                return false;
            }
            return true;
        }

        private static GameObject CreateOrGetDebris(GameObject _toSpawnGameObject,
            List<GameObject> ___debrisPool,
            int ___maxTotalDebris
        )
        {
            if (___debrisPool.Count > ___maxTotalDebris)
            {
                GameObject gameObject = ___debrisPool[0];
                ___debrisPool.RemoveAt(0);
                if (gameObject != null)
                {
                    ___debrisPool.Add(gameObject);
                    gameObject.transform.localScale = Vector3.one;
                    return gameObject;
                }
            }
            GameObject gameObject2 = UnityEngine.Object.Instantiate<GameObject>(_toSpawnGameObject);
            ___debrisPool.Add(gameObject2);
            return gameObject2;
        }

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
