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
using FeatMultiplayer.MessageTypes;

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
                SendWorldObjectToClients(_worldObject, true);
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
                    Destroy(go);
                    _worldObject.ResetPositionAndRotation();
                }

                LogInfo("Dropping: " + DebugWorldObject(_worldObject));
                MessageDropWorldObject msg = new MessageDropWorldObject()
                {
                    id = _worldObject.GetId(),
                    groupId = _worldObject.GetGroup().GetId(),
                    position = _position,
                    dropSize = _dropSize
                };
                SendHost(msg, true);
            }
        }

        /// <summary>
        /// The vanilla game calls WorldObjectsHandler::CreateAndDropOnFloor when an item can't go into
        /// inventory and needs to be dropped at a specific location. By default, this is what
        /// happens when deconstructing buildings with a full inventory or recycling items (which
        /// always puts items into the world).
        /// </summary>
        /// <param name="_group">The group to create an item for.</param>
        /// <param name="_position">where to put the item.</param>
        /// <param name="_dropSize">the size of the dropped item.</param>
        /// <returns>False on the client, true otherwise</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.CreateAndDropOnFloor))]
        static bool WorldObjectsHandler_CreateAndDropOnFloor(Group _group, Vector3 _position, float _dropSize = 0f)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                MessageDropWorldObject msg = new MessageDropWorldObject()
                {
                    id = 0,
                    groupId = _group.GetId(),
                    position = _position,
                    dropSize = _dropSize
                };
                SendHost(msg, true);
                return false;
            }
            return true;
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
                        SendAllClients(messageSetTransform, true);
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
                WorldObject wo = null;
                if (mdwo.id == 0 && !string.IsNullOrEmpty(mdwo.groupId))
                {
                    Group _group = GroupsHandler.GetGroupViaId(mdwo.groupId);
                    if (_group != null)
                    {
                        wo = WorldObjectsHandler.CreateNewWorldObject(_group, 0);
                    }
                    else
                    {
                        LogWarning("ReceiveMessageDropWorldObject: Unknown group " + mdwo.groupId);
                    }
                } else
                {
                    worldObjectById.TryGetValue(mdwo.id, out wo);
                }
                if (wo != null)
                {
                    WorldObjectsHandler.DropOnFloor(wo, mdwo.position, mdwo.dropSize);
                    SendWorldObjectToClients(wo, false);
                    if (TryGetGameObject(wo, out var go))
                    {
                        go.GetComponent<WorldObjectAssociated>()
                            .StartCoroutine(WorldObjectsHandler_DropOnFloor_Tracker(wo));
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageDropWorldObject: Unknown WorldObject " + mdwo.id + ", groupId = " + mdwo.groupId);
                }
            }
        }
    }
}
