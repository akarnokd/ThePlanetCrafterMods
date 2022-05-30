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
        /// The vanilla game just spawns the object in the world, attaches a rigid body and lets it fall.
        /// 
        /// On the host, we let the game do it and then attach a position tracker to the dropped item. This will make
        /// sure the item is roughly at the same visual location on both sides.
        /// 
        /// On the client, we undo the what the original DropOnFloor did, hiding the game object, and
        /// ask the host to drop it for us.
        /// </summary>
        /// <param name="_worldObject">The object to drop onto the floor.</param>
        /// <param name="_position">The initial position where it is dropped.</param>
        /// <param name="_dropSize">Size of the dropped object.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DropOnFloor))]
        static void WorldObjectsHandler_DropOnFloor(WorldObject _worldObject,
            Vector3 _position, float _dropSize = 0f)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                LogInfo("Dropping: " + DebugWorldObject(_worldObject));
                SendWorldObject(_worldObject, true);
                if (TryGetGameObject(_worldObject, out var go))
                {
                    go.GetComponent<WorldObjectAssociated>()
                    .StartCoroutine(WorldObjectsHandler_DropOnFloor_Tracker(_worldObject));
                }
            }
            else
            if (updateMode == MultiplayerMode.CoopClient)
            {
                // Let the host animate it
                if (TryGetGameObject(_worldObject, out var go))
                {
                    UnityEngine.Object.Destroy(go);
                    _worldObject.ResetPositionAndRotation();
                }

                LogInfo("Dropping: " + DebugWorldObject(_worldObject));
                MessageDropWorldObject msg = new MessageDropWorldObject()
                {
                    id = _worldObject.GetId(),
                    position = _position,
                    dropSize = _dropSize
                };
                Send(msg);
                Signal();
            }
        }

        static IEnumerator WorldObjectsHandler_DropOnFloor_Tracker(WorldObject _worldObject)
        {
            float delay = 0.5f / networkFrequency.Value; // sample twice the network frequency
            int maxLoop = (int)(26 / delay);
            for (int i = 0; i < maxLoop; i++)
            {
                if (TryGetGameObject(_worldObject, out var go))
                {
                    if (go != null && _worldObject.GetIsPlaced())
                    {
                        var messageSetTransform = new MessageSetTransform()
                        {
                            id = _worldObject.GetId(),
                            position = go.transform.position,
                            rotation = go.transform.rotation,
                            mode = MessageSetTransform.Mode.GameObjectOnly
                        };
                        Send(messageSetTransform);
                        Signal();
                    }
                    else
                    {
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }
                yield return new WaitForSeconds(delay);
            }

        }

        static void ReceiveMessageDropWorldObject(MessageDropWorldObject mdwo)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (worldObjectById.TryGetValue(mdwo.id, out var wo))
                {
                    WorldObjectsHandler.DropOnFloor(wo, mdwo.position, mdwo.dropSize);
                    SendWorldObject(wo, false);
                    if (TryGetGameObject(wo, out var go))
                    {
                        go.GetComponent<WorldObjectAssociated>()
                            .StartCoroutine(WorldObjectsHandler_DropOnFloor_Tracker(wo));
                    }
                }
            }
        }
    }
}
