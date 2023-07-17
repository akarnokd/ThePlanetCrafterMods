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
        /// Monotonic Id generator for spawns of the outside grower.
        /// </summary>
        static int outsideGrowerSpawnId;

        /// <summary>
        /// Component to hold onto the unique id of the specific spawn
        /// of the outside grower.
        /// </summary>
        class OutsideGrowerSpawnInfo : MonoBehaviour
        {
            /// <summary>
            /// The parent machine identifier
            /// </summary>
            internal int machineId;
            /// <summary>
            /// The unique identifier of this spawn.
            /// </summary>
            internal int spawnId;
            /// <summary>
            /// The index inside the MachineOutsideGrower.thingsToGrow list
            /// </summary>
            internal int typeIndex;
            /// <summary>
            /// Called to trigger a respawn.
            /// </summary>
            internal Action doRespawn;
        }

        /// <summary>
        /// The vanilla game calls MachineOutsideGrower::LaunchGrowingProcess after loading into
        /// a world to spawn the things to grow.
        /// 
        /// On the host, we let this happen as we override MachineOutsideGrower::InstantiateAtRandomPosition
        /// to sync up the spawn info.
        /// 
        /// On the client, we don't let it work and rely on the manual syncing of game objects.
        /// </summary>
        /// <returns>False for client mode, true otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineOutsideGrower), "LaunchGrowingProcess")]
        static bool MachineOutsideGrower_LaunchGrowingProcess()
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        /// <summary>
        /// The vanilla game uses MachineOutsideGrower::InstantiateAtRandomPosition to place the growing
        /// objects around the machine, within a radius, randomly. Also if _fromInit is set,
        /// the spawns will start at the growth percentage defined by the parent MachineOutsideGrower after
        /// loading into a world.
        /// 
        /// On the host, we have to rewrite this method to intercept the spawn's position, give it a trackable
        /// id and send it to the client, as well as handle the grabbing and respawning of it.
        /// 
        /// On the client, we don't allow this routine to run at all. Growth tracking will be done separately.
        /// </summary>
        /// <param name="__instance">The GameObject of the grower.</param>
        /// <param name="_objectToInstantiate">The gameobject representing the thing to spawn</param>
        /// <param name="_fromInit">Is this the initialization spawn after loading in?</param>
        /// <param name="___worldObjectGrower">The parent machine outside grower world object</param>
        /// <param name="___instantiatedGameObjects">List of already instantiated game objects by the grower</param>
        /// <param name="___radius">The spawn radius</param>
        /// <returns>False if in multiplayer, true otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineOutsideGrower), "InstantiateAtRandomPosition")]
        static bool MachineOutsideGrower_InstantiateAtRandomPosition(
            MachineOutsideGrower __instance,
            GameObject _objectToInstantiate,
            bool _fromInit,
            WorldObject ___worldObjectGrower,
            List<GameObject> ___instantiatedGameObjects,
            float ___radius,
            bool ___spawnOnTerrain,
            bool ___spawnOnWater,
            bool ___canRecolt,
            GameObject ___grownThingsContainer,
            bool ___alignWithNormal,
            float ___growSize,
            GameObject ___spawnOnThis,
            float ___downValue,
            int ___canGrabAtXPercent,
            MachineOutsideGrowerSpecificRadius ___machineOutsideGrowerSpecificRadius,
            Inventory ___inventory
        )
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                int layerMask = ~LayerMask.GetMask(GameConfig.commonIgnoredLayers);
                
                float radiusForObject = ___radius;
                if (___machineOutsideGrowerSpecificRadius != null)
                {
                    int num2 = __instance.thingsToGrow.IndexOf(_objectToInstantiate);
                    if (___machineOutsideGrowerSpecificRadius.GetRadiusForObject(num2) != 0f)
                    {
                        radiusForObject = ___machineOutsideGrowerSpecificRadius.GetRadiusForObject(num2);
                    }
                }
                Vector2 randomPointInRadius = UnityEngine.Random.insideUnitCircle * radiusForObject;

                var go = __instance.gameObject;
                Vector3 relativeToGrower = new Vector3(
                    go.transform.position.x + randomPointInRadius.x,
                    go.transform.position.y,
                    go.transform.position.z + randomPointInRadius.y);

                Vector3 raycastFrom = new Vector3(relativeToGrower.x, relativeToGrower.y + 10f, relativeToGrower.z);
                Vector3 raycastdirection = Vector3.up * -1f;

                if (Physics.Raycast(new Ray(raycastFrom, raycastdirection), out var raycastHit, 100f, layerMask))
                {
                    bool validLocation = false;
                    if (___spawnOnTerrain && raycastHit.collider.gameObject.GetComponent<Terrain>() != null)
                    {
                        validLocation = true;
                    }
                    if (___spawnOnWater && raycastHit.collider.gameObject.layer == LayerMask.NameToLayer(GameConfig.layerWaterName))
                    {
                        validLocation = true;
                    }
                    if (___spawnOnThis != null && raycastHit.collider.gameObject == ___spawnOnThis)
                    {
                        validLocation = true;
                    }
                    if (!validLocation)
                    {
                        return false;
                    }

                    GameObject spawn = Instantiate<GameObject>(_objectToInstantiate, ___grownThingsContainer.transform);
                    spawn.transform.position = raycastHit.point;
                    if (!___canRecolt)
                    {
                        GameObjects.RemoveCollidersOnChildren(spawn, false);
                    }

                    int id = ++outsideGrowerSpawnId;
                    var spi = spawn.AddComponent<OutsideGrowerSpawnInfo>();
                    spi.spawnId = id;
                    spi.typeIndex = __instance.thingsToGrow.IndexOf(_objectToInstantiate);
                    spi.machineId = ___worldObjectGrower.GetId();

                    var spawnPosition = spawn.transform.position;
                    spawn.transform.position = new Vector3(spawnPosition.x, spawnPosition.y - ___downValue, spawnPosition.z);

                    float z = UnityEngine.Random.value * 360f;
                    Quaternion lhs = Quaternion.Euler(0f, 0f, z);
                    Quaternion rotation = Quaternion.LookRotation(___alignWithNormal ? raycastHit.normal : Vector3.up) * (lhs * Quaternion.Euler(90f, 0f, 0f));
                    spawn.transform.rotation = rotation;

                    float spawnScaling = 0f;
                    if (_fromInit)
                    {
                        spawnScaling = ___growSize * ___worldObjectGrower.GetGrowth() / 100f;
                    }
                    spawn.transform.localScale = new Vector3(spawnScaling, spawnScaling, spawnScaling);

                    var woa = spawn.GetComponent<WorldObjectAssociated>();
                    if (woa != null)
                    {
                        var woSpawn = woa.GetWorldObject();
                        if (woSpawn != null)
                        {
                            woSpawn.SetPositionAndRotation(spawn.transform.position, Quaternion.identity);
                            //SendWorldObjectToClients(woSpawn, false);
                        }
                    }

                    var ag = spawn.GetComponent<ActionGrabable>();
                    if (ag != null)
                    {
                        ag.SetCanGrab(___canGrabAtXPercent <= 100 * spawnScaling / ___growSize);

                        spi.doRespawn = () =>
                        {
                            if (!__instance.gameObject.activeInHierarchy)
                            {
                                return;
                            }
                            ___instantiatedGameObjects.Remove(spawn);
                            if (___inventory != null && ___inventory.GetInsideWorldObjects().Count != 0)
                            {

                                machineOutsideGrowerInstantiateAtRandomPosition.Invoke(__instance, new object[] { _objectToInstantiate, false });

                                __instance.StopAllCoroutines();
                                var enumer = (IEnumerator)machineOutsideGrowerUpdateGrowing.Invoke(__instance, new object[] { __instance.updateInterval });
                                __instance.StartCoroutine(enumer);
                            }
                        };

                        ag.grabedEvent = new Grabed((wo, notif) => OnGrabSpawn(spi.machineId, id, spi.doRespawn));
                    }
                    ___instantiatedGameObjects.Add(spawn);

                    LogInfo("MachineOutsideGrower: Spawn new " + spi.machineId + ", " + id/* + "\n" + Environment.StackTrace*/);
                    SendAllClients(new MessageGrowAdd()
                    {
                        machineId = spi.machineId,
                        spawnId = id,
                        typeIndex = spi.typeIndex,
                        growth = spawnScaling,
                        growSize = ___growSize,
                        position = spawn.transform.position,
                        rotation = spawn.transform.rotation,
                    }, true);
                }

                return false;
            }
            return updateMode != MultiplayerMode.CoopClient;
        }

        static void OnGrabSpawn(int machineId, int spawnId, Action respawn)
        {
            SendAllClients(new MessageGrowRemove()
            {
                machineId = machineId,
                spawnId = spawnId,
            }, true);
            respawn();
        }

        /// <summary>
        /// The vanilla game uses MachineOutsideGrower::Grow to update the spawns up until all
        /// of them have grown to 100%.
        /// 
        /// On the host, we need to intercept the individual size changes and send it to the client.
        /// 
        /// On the client, we don't allow the original to run at all and handle the growth in
        /// ReceiveMessageGrowAdd instead.
        /// </summary>
        /// <param name="__instance">The outside grower's game object</param>
        /// <param name="___instantiatedGameObjects">list of already instantiated spawns</param>
        /// <param name="___hasEnergy">true if the grower receives energy</param>
        /// <param name="___worldObjectGrower">The world object of the outside grower</param>
        /// <param name="___growSize">How big the spawn should grow?</param>
        /// <param name="___growSpeed">How fast the spawn should grow</param>
        /// <returns>False for multiplayer, true for singleplayer</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineOutsideGrower), "Grow")]
        static bool MachineOutsideGrower_Grow(
            MachineOutsideGrower __instance,
            List<GameObject> ___instantiatedGameObjects,
            bool ___hasEnergy,
            WorldObject ___worldObjectGrower,
            float ___growSize,
            float ___growSpeed,
            int ___canGrabAtXPercent
        )
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (___instantiatedGameObjects != null && ___instantiatedGameObjects.Count > 0 && ___hasEnergy)
                {
                    bool allFullGrown = true;
                    foreach (GameObject spawn in ___instantiatedGameObjects)
                    {
                        if (spawn != null)
                        {
                            float growScale = spawn.transform.localScale.x;
                            if (growScale <= ___growSize)
                            {
                                float num = ___growSpeed * UnityEngine.Random.Range(0f, 1f);
                                spawn.transform.localScale += new Vector3(num, num, num);

                                allFullGrown = false;
                                foreach (VegetationTree tree in spawn.GetComponentsInChildren<VegetationTree>())
                                {
                                    tree.UpdateConditions();
                                }
                            }
                            var growthPercent = 100 * growScale / ___growSize;
                            ___worldObjectGrower.SetGrowth(growthPercent);
                            var ag = spawn.GetComponent<ActionGrabable>();
                            if (ag != null)
                            {
                                ag.SetCanGrab(___canGrabAtXPercent <= growthPercent);
                            }

                            var spi = spawn.GetComponent<OutsideGrowerSpawnInfo>();

                            SendAllClients(new MessageGrowAdd()
                            {
                                machineId = ___worldObjectGrower.GetId(),
                                spawnId = spi.spawnId,
                                typeIndex = spi.typeIndex,
                                growth = spawn.transform.localScale.x,
                                growSize = ___growSize,
                                position = spawn.transform.position,
                                rotation = spawn.transform.rotation
                            });
                            /*
                            LogInfo("MachineOutsideGrower_Grow: " + ___worldObjectGrower.GetId() 
                                + ", " + spi.spawnId 
                                + ", " + growthPercent
                                + ", " + spawn.transform.position); 
                            */
                        }
                    }
                    SignalAllClients();
                    if (allFullGrown)
                    {
                        ___worldObjectGrower.SetGrowth(100f);
                        // keep sending growth messages
                        //__instance.StopAllCoroutines();
                    }
                }
                return false;
            }
            return updateMode != MultiplayerMode.CoopClient;
        }

        static void ReceiveMessageGrowAdd(MessageGrowAdd mga)
        {
            if (worldObjectById.TryGetValue(mga.machineId, out var wo))
            {
                if (TryGetGameObject(wo, out var go))
                {
                    var mog = go.GetComponent<MachineOutsideGrower>();

                    if (mog.thingsToGrow.Count > mga.typeIndex)
                    {
                        foreach (Transform espawn in mog.grownThingsContainer.transform)
                        {
                            var sid = espawn.GetComponent<OutsideGrowerSpawnInfo>();
                            if (sid != null && sid.spawnId == mga.spawnId)
                            {
                                /*
                                LogInfo("ReceiveMessageGrowAdd: " + mga.machineId
                                    + ", " + mga.spawnId
                                    + ", " + (mga.growth * 100 / mga.growSize)
                                    + ", " + espawn.transform.position
                                    + ", " + espawn.gameObject.activeSelf
                                    );
                                */
                                espawn.transform.localScale = new Vector3(mga.growth, mga.growth, mga.growth);
                                foreach (VegetationTree tree in espawn.GetComponentsInChildren<VegetationTree>())
                                {
                                    tree.UpdateConditions();
                                }
                                var ag0 = espawn.GetComponent<ActionGrabable>();
                                if (ag0 != null)
                                {
                                    ag0.SetCanGrab(mog.canGrabAtXPercent <= 100 * mga.growth / mga.growSize);
                                }
                                return;
                            }
                        }

                        var spawn = Instantiate(mog.thingsToGrow[mga.typeIndex], mog.grownThingsContainer.transform);
                        // detach the game object from the world object
                        var woa = spawn.GetComponent<WorldObjectAssociated>();
                        if (woa != null)
                        {
                            var wog = woa.GetWorldObject();
                            if (wog != null)
                            {
                                wog.SetGameObject(null);
                            }
                        }

                        spawn.transform.position = mga.position;
                        spawn.transform.rotation = mga.rotation;

                        /*
                        LogInfo("ReceiveMessageGrowAdd: " + mga.machineId
                            + ", new " + mga.spawnId
                            + ", " + (mga.growth * 100 / mga.growSize)
                            + ", " + spawn.transform.position
                            + ", " + mga.position
                            );
                        */

                        if (!mog.canRecolt)
                        {
                            GameObjects.RemoveCollidersOnChildren(spawn, false);
                        }

                        var scaling = mga.growth;
                        spawn.transform.localScale = new Vector3(scaling, scaling, scaling);

                        var spi = spawn.AddComponent<OutsideGrowerSpawnInfo>();
                        spi.spawnId = mga.spawnId;
                        spi.typeIndex = mga.typeIndex;
                        spi.machineId = mga.machineId;

                        var ag = spawn.GetComponent<ActionGrabable>();
                        if (ag != null)
                        {
                            ag.SetCanGrab(mog.canGrabAtXPercent <= mga.growth * 100 / mga.growSize);
                        }

                        
                    }
                    else
                    {
                        LogError("ReceiveMessageGrowAdd: Can't spawn because thingsToGrow indexing is off: " + mga.machineId + " -> " + mga.spawnId + " @ " + mga.typeIndex);
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageGrowAdd: GameObject not found for WorldObject " + DebugWorldObject(wo));
                }
            }
            else
            {
                LogWarning("ReceiveMessageGrowAdd: WorldObject not found: " + mga.machineId);
            }
        }

        static void ReceiveMessageGrowRemove(MessageGrowRemove mgr)
        {
            if (worldObjectById.TryGetValue(mgr.machineId, out var wo))
            {
                if (TryGetGameObject(wo, out var go))
                {
                    var mog = go.GetComponent<MachineOutsideGrower>();
                    var tr = mog.grownThingsContainer.transform;

                    for (int i = tr.childCount - 1; i >= 0; i--)
                    {
                        var spawn = tr.GetChild(i);
                        var sid = spawn.GetComponent<OutsideGrowerSpawnInfo>();
                        if (sid != null)
                        {
                            if (sid.spawnId == mgr.spawnId)
                            {
                                if (updateMode == MultiplayerMode.CoopHost)
                                {
                                    WorldObjectAssociated woa = spawn.GetComponent<WorldObjectAssociated>();
                                    if (woa != null)
                                    {
                                        WorldObject woSpawn = woa.GetWorldObject();
                                        woSpawn.ResetPositionAndRotation();
                                        LogInfo("ReceiveMessageGrowRemove: Grabbing: " + mgr.machineId + " -> "
                                            + mgr.spawnId + " -> " + DebugWorldObject(woSpawn));

                                        SendWorldObjectToClients(woSpawn, false);
                                        Inventory inv = mgr.sender.shadowBackpack;
                                        if (inv.AddItem(woSpawn))
                                        {
                                            woSpawn.SetDontSaveMe(false);
                                            mgr.sender.Send(mgr);
                                            mgr.sender.Signal();

                                            sid.doRespawn?.Invoke();
                                            var ag = sid.GetComponent<ActionGrabable>();
                                            if (ag != null)
                                            {
                                                ag.grabedEvent = null; // prevent duplicate spawns because of ActionGrabable::OnDestroy
                                            }
                                            Destroy(spawn.gameObject);
                                        }
                                        else
                                        {
                                            WorldObjectsHandler.DestroyWorldObject(woSpawn);
                                            woa.SetWorldObject(null);
                                        }
                                    }
                                }
                                else
                                {
                                    LogInfo("ReceiveMessageGrowRemove: Removing: " + mgr.machineId + " -> " + mgr.spawnId);
                                    Destroy(spawn.gameObject);
                                }
                                return;
                            }
                        }
                    }

                    LogWarning("ReceiveMessageGrowRemove: Spawn not found: " + mgr.machineId + " -> " + mgr.spawnId);
                }
                else
                {
                    LogWarning("ReceiveMessageGrowRemove: GameObject not found for WorldObject " + DebugWorldObject(wo));
                }
            }
            else
            {
                LogWarning("ReceiveMessageGrowRemove: WorldObject not found: " + mgr.machineId);
            }
        }
    }
}
