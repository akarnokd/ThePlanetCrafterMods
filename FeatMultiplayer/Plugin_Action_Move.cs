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
            __instance.StartCoroutine(DoorsReaction(___door1, ___door2));
        }

        /// <summary>
        /// Distance in units how close the avatar has to be to the door.
        /// </summary>
        static float doorActivationDistance = 2f;
        /// <summary>
        /// How often to check the player avatar's position, in seconds.
        /// </summary>
        static float doorActivationCheckPeriod = 0.05f;

        static IEnumerator DoorsReaction(Door door1, Door door2)
        {
            bool entered = false;
            var d1 = door1.transform.position;
            var d2 = door2.transform.position;
            var d3 = d1 + (d2 - d1) / 2;
            for (; ; )
            {
                if (otherPlayer != null)
                {
                    var pos = otherPlayer.avatar.transform.position;


                    if (Vector3.Distance(pos, d3) <= doorActivationDistance)
                    {
                        if (!entered)
                        {
                            entered = true;
                            door1.OpenDoor();
                            door2.OpenDoor();
                        }
                    }
                    else
                    if (entered)
                    {
                        entered = false;
                        // FIXME what if the main player also stands there?!
                        door1.CloseDoor();
                        door2.CloseDoor();
                    }
                }
                else
                {
                    if (entered)
                    {
                        entered = false;
                        door1.CloseDoor();
                        door2.CloseDoor();
                    }
                }
                yield return new WaitForSeconds(doorActivationCheckPeriod);
            }
        }

        void SendPlayerLocation()
        {
            PlayerMainController pm = GetPlayerMainController();
            if (pm != null)
            {
                Transform player = pm.transform;
                MessagePlayerPosition mpp = new MessagePlayerPosition
                {
                    position = player.position,
                    rotation = player.rotation
                };
                Send(mpp);
                Signal();
            }
        }

        static void ReceivePlayerLocation(MessagePlayerPosition mpp)
        {
            otherPlayer?.SetPosition(mpp.position, mpp.rotation);
        }
    }
}
