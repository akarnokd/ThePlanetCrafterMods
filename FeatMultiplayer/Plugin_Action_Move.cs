using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game uses a collider to detect when the player nears or leaves the door, handled by this DoorsHandler.
        /// Unfortunately, the other player's avatar does not trigger the door collision as only objects with RigidBody component
        /// would do. However, if added to the avatar, it behaves erratically as it tries to fall over.
        /// The workaround is to simply track the avatar's position and when enters/leaves the door's range, trigger the opening and closing.
        /// </summary>
        /// <param name="__instance">The handler instance to use as a host for the periodic checker coroutine.</param>
        /// <param name="___door1">One door panel</param>
        /// <param name="___door2">The other door panel</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DoorsHandler), "Start")]
        static void DoorsHandler_Start(DoorsHandler __instance, Door ___door1, Door ___door2)
        {
            if (updateMode == MultiplayerMode.CoopHost || updateMode == MultiplayerMode.CoopClient)
            {
                __instance.StartCoroutine(DoorsReaction(___door1, ___door2, __instance.GetComponentInChildren<Collider>()));
            }
        }

        /// <summary>
        /// The vanilla calls this when the user leaves the collision box of the door.
        /// 
        /// We bypass this in multiplayer so the doors open and close with
        /// considering all players nearby.
        /// </summary>
        /// <returns>True for singleplayer, false for multiplayer.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DoorsHandler), "OnTriggerEnter")]
        static bool DoorsHandler_OnTriggerEnter()
        {
            return updateMode == MultiplayerMode.SinglePlayer;
        }

        /// <summary>
        /// The vanilla calls this when the user leaves the collision box of the door.
        /// 
        /// We bypass this in multiplayer so the doors open and close with
        /// considering all players nearby.
        /// </summary>
        /// <returns>True for singleplayer, false for multiplayer.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DoorsHandler), "OnTriggerExit")]
        static bool DoorsHandler_OnTriggerExit()
        {
            return updateMode == MultiplayerMode.SinglePlayer;
        }

        static IEnumerator DoorsReaction(Door door1, Door door2, Collider collider)
        {
            bool entered = false;

            for (; ; )
            {
                var localPosition = GetPlayerMainController().transform.position;
                var otherPosition = otherPlayer != null ? otherPlayer.rawPosition : localPosition;

                var localInRange = collider.bounds.Contains(localPosition);
                var otherInRange = collider.bounds.Contains(otherPosition);

                if (!entered && (localInRange || otherInRange))
                {
                    entered = true;
                    door1.OpenDoor();
                    door2.OpenDoor();
                }
                else
                if (entered && !localInRange && !otherInRange)
                {
                    entered = false;
                    door1.CloseDoor();
                    door2.CloseDoor();
                }

                
                yield return 0;
            }
        }

        void SendPlayerLocation()
        {
            PlayerMainController pm = GetPlayerMainController();
            if (pm != null)
            {
                var lightMode = 0;
                var mtl = pm.GetComponentInChildren<MultiToolLight>();
                if (mtl != null)
                {
                    if (mtl.toolLightT2.activeSelf)
                    {
                        lightMode = 2;
                    }
                    else
                    if (mtl.toolLightT1.activeSelf)
                    {
                        lightMode = 1;
                    }
                }
                Transform player = pm.transform;
                var camera = pm.GetComponentInChildren<PlayerLookable>();
                MessagePlayerPosition mpp = new MessagePlayerPosition
                {
                    position = player.position,
                    rotation = camera.m_Camera.transform.rotation,
                    lightMode = lightMode
                };
                Send(mpp);
                Signal();
            }
        }

        static void ReceivePlayerLocation(MessagePlayerPosition mpp)
        {
            otherPlayer?.SetPosition(mpp.position, mpp.rotation, mpp.lightMode);
        }

        /// <summary>
        /// The vanilla game uses SectorEnter::Start to set up a collision box around load boundaries (sectors).
        /// 
        /// On the host, we track the other player and load in the respective sector.
        /// 
        /// On the client, we don't care where the host is?!
        /// </summary>
        /// <param name="__instance">The parent SectorEnter to register a coroutine tracker on.</param>
        /// <param name="___collider">The collision box object around the sector</param>
        /// <param name="___sector">The sector itself so we know what scene to load via <see cref="SceneManager.LoadSceneAsync(int, LoadSceneMode)"/></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SectorEnter), "Start")]
        static void SectorEnter_Start(SectorEnter __instance, Collider ___collider, Sector ___sector)
        {
            LogInfo("SectorEnter_Start: " + ___sector.gameObject.name);
            __instance.StartCoroutine(SectorEnter_TrackOtherPlayer(___collider, ___sector));
        }

        /// <summary>
        /// How often to check the collisions with the other player.
        /// </summary>
        static float sectorTrackDelay = 0.5f;
        /// <summary>
        /// If the player triggered a scene load, defer the spawns.
        /// </summary>
        static bool sceneLoadingForSpawn;
        /// <summary>
        /// What inventory generation requests are pending due to scene loading.
        /// </summary>
        static HashSet<int> sceneSpawnRequest = new();

        static IEnumerator SectorEnter_TrackOtherPlayer(Collider ___collider, Sector ___sector)
        {
            bool otherEntered = false;
            for (; ; )
            {
                if (updateMode == MultiplayerMode.CoopHost && otherPlayer != null)
                {
                    string name = ___sector.gameObject.name;
                    if (___collider.bounds.Contains(otherPlayer.rawPosition))
                    {
                        if (!otherEntered)
                        {
                            otherEntered = true;
                            LogInfo("SectorEnter_TrackOtherPlayer: other player entered " + name);
                        }
                        Scene scene = SceneManager.GetSceneByName(name);
                        if (!scene.IsValid())
                        {
                            sceneLoadingForSpawn = true;
                            LogInfo("SectorEnter_TrackOtherPlayer: Loading Scene " + name);
                            var aop = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                            aop.completed += (op) => OnSceneLoadedForSpawn(op, ___sector, name);
                        }
                    }
                    else
                    {
                        if (otherEntered)
                        {
                            otherEntered = false;
                            LogInfo("SectorEnter_TrackOtherPlayer: other player exited " + name);
                        }
                    }

                }
                yield return new WaitForSeconds(sectorTrackDelay);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Sector), "LoadSector")]
        static bool Sector_LoadSector(Sector __instance, List<GameObject> ___decoyGameObjects)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                string name = __instance.gameObject.name;

                Scene scene = SceneManager.GetSceneByName(name);
                if (!scene.IsValid())
                {
                    LogInfo("Sector_LoadSector " + name + " Loading");
                    var aop = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                    aop.completed += (ao) =>
                    {
                        LogInfo("Sector_LoadSector " + name + " Loading Done");
                        foreach (var go in ___decoyGameObjects)
                        {
                            go.SetActive(false);
                        }
                    };
                } else
                {
                    LogInfo("Sector_LoadSector " + name + ", Decoys = " + ___decoyGameObjects.Count + " already loaded");
                }
                return false;
            }
            return true;
        }

        /*
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Sector), "SceneLoaded")]
        static void Sector_SceneLoaded(Sector __instance)
        {
            LogInfo("Sector_SceneLoaded " + __instance.gameObject.name);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Sector), "SceneUnLoaded")]
        static void Sector_SceneUnLoaded(Sector __instance)
        {
            LogInfo("Sector_SceneUnLoaded " + __instance.gameObject.name);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Sector), "UnloadSector")]
        static void Sector_UnloadSector(Sector __instance, List<GameObject> ___decoyGameObjects)
        {
            LogInfo("Sector_UnloadSector " + __instance.gameObject.name + ", Decoys = " + ___decoyGameObjects.Count);
        }
        */
    }
}
