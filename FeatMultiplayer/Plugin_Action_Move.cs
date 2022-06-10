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

        /// <summary>
        /// Distance in units how close the avatar has to be to the door.
        /// </summary>
        static float doorActivationDistance = 3f;

        static IEnumerator DoorsReaction(Door door1, Door door2, Collider collider)
        {
            bool entered = false;
            var d1 = door1.transform.position;
            var d2 = door2.transform.position;
            var d3 = d1 + (d2 - d1) / 2;

            for (; ; )
            {
                var localPosition = GetPlayerMainController().transform.position;
                var otherPosition = otherPlayer != null ? otherPlayer.rawPosition : localPosition;

                /*
                var localDist = Vector3.Distance(localPosition, d3);
                var otherDist = Vector3.Distance(otherPosition, d3);

                var localInRange = localDist <= doorActivationDistance;
                var otherInRange = otherDist <= doorActivationDistance;
                */

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
    }
}
