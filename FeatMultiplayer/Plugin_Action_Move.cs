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
using FeatMultiplayer.MessageTypes;

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

                if (collider != null && door1 != null && door2 != null)
                {
                    var localInRange = collider.bounds.Contains(localPosition);
                    var otherInRange = false;
                    foreach (var avatar in playerAvatars)
                    {
                        if (collider.bounds.Contains(avatar.Value.rawPosition))
                        {
                            otherInRange = true;
                            break;
                        }
                    }

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

                }

                yield return null;
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

                var localWalkMode = playerWalkModeSnapshot;
                playerWalkModeSnapshot = 0;

                if (pm.GetPlayerAudio().soundJetPack.isPlaying)
                {
                    localWalkMode |= PlayerAvatar.MoveEffect_Jetpacking;
                }

                MessagePlayerPosition mpp = new MessagePlayerPosition
                {
                    position = player.position,
                    rotation = camera.m_Camera.transform.rotation,
                    lightMode = lightMode,
                    clientName = "", // not used when sending
                    miningPosition = playerMiningTarget != null ? playerMiningTarget.transform.position : Vector3.zero,
                    walkMode = localWalkMode
                };

                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendAllClients(mpp, true);
                }
                else
                {
                    SendHost(mpp, true);
                }
            }
        }

        static void ReceivePlayerLocation(MessagePlayerPosition mpp)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var clientName = mpp.sender.clientName;
                if (clientName != null)
                {
                    if (playerAvatars.TryGetValue(clientName, out var avatar))
                    {
                        avatar.UpdateState(mpp.position, mpp.rotation, mpp.lightMode, mpp.miningPosition, mpp.walkMode);

                        mpp.clientName = clientName;

                        SendAllClientsExcept(mpp.sender.id, mpp, true);
                    }
                }
            }
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (playerAvatars.TryGetValue(mpp.clientName, out var avatar))
                {
                    avatar.UpdateState(mpp.position, mpp.rotation, mpp.lightMode, mpp.miningPosition, mpp.walkMode);
                }
            }

            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (mpp.sender.shadowBackpack != null)
                {
                    StorePlayerPosition(mpp.sender.clientName, mpp.sender.shadowBackpackWorldObjectId, mpp.position);
                }
            }
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
                if (updateMode == MultiplayerMode.CoopHost && playerAvatars.Count != 0)
                {
                    string name = ___sector.gameObject.name;
                    bool found = false;
                    foreach (var avatar in playerAvatars)
                    {
                        if (___collider.bounds.Contains(avatar.Value.rawPosition))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        if (!otherEntered)
                        {
                            otherEntered = true;
                            LogInfo("SectorEnter_TrackOtherPlayer: any other player entered " + name);
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
                            LogInfo("SectorEnter_TrackOtherPlayer: all other players exited " + name);
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

        public void ReceiveMessageMovePlayer(MessageMovePlayer mmp)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                LogInfo("Moved by host to " + mmp.position);
                PlayerMainController pm = GetPlayerMainController();
                pm.SetPlayerPlacement(mmp.position, pm.transform.rotation);
            }
        }

        internal static float playerAudioSoundLoopTimeSteps;
        internal static AudioResourcesHandler playerAudioAudioResourcesHandler;
        internal static int playerWalkModeSnapshot;

        /// <summary>
        /// Vanilla initializes the player audio handler.
        /// 
        /// We need the references to the resources it uses. Not expected to change
        /// during runtime.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerAudio), "Start")]
        static void PlayerAudio_Start(float ___soundLoopTimeSteps, AudioResourcesHandler ___audioResourcesHandler)
        {
            playerAudioSoundLoopTimeSteps = ___soundLoopTimeSteps;
            playerAudioAudioResourcesHandler = ___audioResourcesHandler;
        }

        /// <summary>
        /// Vanilla plays one of the clips of the given array.
        /// 
        /// We figure out which set it was, then record it so clients can sync their audio effect.
        /// </summary>
        /// <param name="_stepsArray"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerAudio), "PlayRandomStep")]
        static void PlayerAudio_PlayRandomStep(List<AudioClip> _stepsArray, bool ___isRunning, bool ___isUnderWater)
        {
            playerWalkModeSnapshot = 0;
            if (playerAudioAudioResourcesHandler != null)
            {
                if (_stepsArray == playerAudioAudioResourcesHandler.walkOnSand)
                {
                    playerWalkModeSnapshot = 1;
                }
                else if (_stepsArray == playerAudioAudioResourcesHandler.walkOnMetal)
                {
                    playerWalkModeSnapshot = 2;
                }
                else if (_stepsArray == playerAudioAudioResourcesHandler.walkOnWood)
                {
                    playerWalkModeSnapshot = 3;
                }
                else if (_stepsArray == playerAudioAudioResourcesHandler.walkOnWater)
                {
                    playerWalkModeSnapshot = 4;
                }
                else if (_stepsArray == playerAudioAudioResourcesHandler.swimming)
                {
                    playerWalkModeSnapshot = 5;
                }

                if (___isRunning)
                {
                    playerWalkModeSnapshot |= PlayerAvatar.MoveEffect_Running;
                }
                if (___isUnderWater)
                {
                    playerWalkModeSnapshot |= PlayerAvatar.MoveEffect_Swimming;
                }
            }
        }

        /// <summary>
        /// The vanilla plays the fall damage sound.
        /// 
        /// We send it to the others as part of the walk mode sync
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerAudio), nameof(PlayerAudio.PlayFallDamage))]
        static void PlayerAudio_PlayFallDamage()
        {
            playerWalkModeSnapshot |= PlayerAvatar.MoveEffect_FallDamage;
        }
    }
}
