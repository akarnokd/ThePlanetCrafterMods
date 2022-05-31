using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// If true, the <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c>
        /// mod has been detected and its generation routine overridden.
        /// </summary>
        static bool modMachineRemoteDeposit;

        static FieldInfo machineGrowerInstantiatedGameObject;
        static FieldInfo machineGrowerInventory;

        /// <summary>
        /// If the mod <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c>
        /// is found, install the deposit override callback.
        /// 
        /// If the mod <c>akarnokd.theplanetcraftermods.cheatautoharvest</c>
        /// is found, install a callback to disable it on the client.
        /// </summary>
        static void TryInstallMachineOverrides()
        {
            if (Chainloader.PluginInfos.TryGetValue(modCheatMachineRemoteDepositGuid, out var pi))
            {
                AccessTools.Field(pi.Instance.GetType(), "overrideDeposit")
                    .SetValue(null, new Func<Inventory, string, bool>(GenerateAnObjectAndDepositInto));
                modMachineRemoteDeposit = true;
                LogInfo("Mod " + modCheatMachineRemoteDepositGuid + " Found, Overriding its deposit logic");
            }
            else
            {
                LogInfo("Mod " + modCheatMachineRemoteDepositGuid + " Not found");
            }

            if (Chainloader.PluginInfos.TryGetValue(modCheatAutoHarvestGuid, out pi))
            {
                AccessTools.Field(pi.Instance.GetType(), "canExecute")
                    .SetValue(null, new Func<bool>(AllowAutoHarvest));
                LogInfo("Mod " + modCheatAutoHarvestGuid + " Found, Overriding its deposit logic");

                // I think nothing else needs overridden because the grower's algae already have
                // a WorldObject, and the depositer only moves them to inventory - handled separately
            }
            else
            {
                LogInfo("Mod " + modCheatAutoHarvestGuid + " Not found");
            }

            machineGrowerInstantiatedGameObject = AccessTools.Field(typeof(MachineGrower), "instantiatedGameObject");
            machineGrowerInventory = AccessTools.Field(typeof(MachineGrower), "inventory");
        }

        /// <summary>
        /// The vanilla game's machines, such as the miners, the water collectors and the gas
        /// extractors use MachineGenerator::GenerateAnObject to generate an object and add it
        /// to the machine's own inventory.
        /// 
        /// On the host, we override and reproduce the behavior to get to the world object and
        /// send it to the client.
        /// 
        /// On the client, we don't let it generate any object so the host can generate it.
        /// 
        /// If the mod <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c> is
        /// detected, the deposit is overridden and the method
        /// <see cref="GenerateAnObjectAndDepositInto(Inventory, string)"/> will handle
        /// the dispatching of the WorldObject and inventory change.
        /// </summary>
        /// <returns>True in singleplayer and if the mod is installed so it can run, false otherwise.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGenerator), "GenerateAnObject")]
        static bool MachineGenerator_GenerateAnObject(List<Group> ___groupDatas, Inventory ___inventory)
        {
            if (!modMachineRemoteDeposit)
            {
                if (updateMode == MultiplayerMode.CoopHost)
                {
                    string oreId = ___groupDatas[UnityEngine.Random.Range(0, ___groupDatas.Count)].id;
                    GenerateAnObjectAndDepositInto(___inventory, oreId);
                    return false;
                }
                else
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Called by the mod <c>akarnokd.theplanetcraftermods.cheatmachineremotedeposit</c>
        /// in its <c>MachineGenerator_GenerateAnObject</c> patched method when
        /// it was determined what to deposit to which inventory.
        /// </summary>
        /// <param name="inv">The target inventory.</param>
        /// <param name="oreId">The ore to create and deposit.</param>
        /// <returns>True if the deposit was handled, false if the original mod should handle it</returns>
        static bool GenerateAnObjectAndDepositInto(Inventory inv, string oreId)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                return true;
            }
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var wo = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId(oreId));
                suppressInventoryChange = true;
                bool added;
                try
                {
                    added = inv.AddItem(wo);
                }
                finally
                {
                    suppressInventoryChange = false;
                }

                if (added)
                {
                    // We need to send the object first, then send the instruction that it has been
                    // Added to the target inventory.
                    SendWorldObject(wo, false);
                    Send(new MessageInventoryAdded()
                    {
                        inventoryId = inv.GetId(),
                        itemId = wo.GetId(),
                        groupId = oreId
                    });
                    Signal();
                }
                else
                {
                    WorldObjectsHandler.DestroyWorldObject(wo);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Called by the mod <c>akarnokd.theplanetcraftermods.cheatautoharvest</c> to
        /// check if its deposit routine should run.
        /// For clients, it should not.
        /// </summary>
        /// <returns>False for clients, true otherwise</returns>
        static bool AllowAutoHarvest()
        {
            return updateMode != MultiplayerMode.CoopClient;
        }

        // FIXME this grower stuff

        /// <summary>
        /// The vanilla game uses the MachineGrower::UpdateSize enumerator to periodically update
        /// the growth of the mockup vegetable and to instantiate the final, grabable vegetable.
        /// 
        /// On the host, we need to rewrite the behavior so we can capture the created world object,
        /// then send it to the client. Also we need to send the growth progress to the client.
        /// 
        /// On the client, we don't grow the mockup vegetable but update it's size based on the
        /// WorldObject::GetGrowth received from the host.
        /// </summary>
        /// <param name="__result">Set the result to the new enumerator if not-singleplayer</param>
        /// <param name="__instance">The parent MachineGrower to interact with</param>
        /// <param name="___worldObjectGrower">The WorldObject of the parent MachineGrower</param>
        /// <param name="___inventory">The inventory of the machine grower</param>
        /// <param name="___updateSizeInterval">How often to update the growth state?</param>
        /// <returns>true if not single player, false to override the behavior with our custom enumerator.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrower), "UpdateSize")]
        static bool MachineGrower_UpdateSize(
            ref IEnumerator __result,
            MachineGrower __instance,
            WorldObject ___worldObjectGrower,
            Inventory ___inventory,
            float ___updateSizeInterval)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                __result = MachineGrower_UpdateSize(__instance, ___worldObjectGrower, ___updateSizeInterval, ___inventory);
                return false;
            }
            return true;
        }

        static IEnumerator MachineGrower_UpdateSize(
            MachineGrower instance,
            WorldObject growerMachine, 
            float updateSizeInterval,
            Inventory inventory
            )
        {
            for (; ; )
            {
                GameObject goMockup = (GameObject)machineGrowerInstantiatedGameObject.GetValue(instance);
                if (goMockup != null)
                {
                    float growth = growerMachine.GetGrowth();
                    if (updateMode == MultiplayerMode.CoopClient)
                    {
                        if (growth < 100f)
                        {
                            float scale = Mathf.Max(0f, Math.Min(1f, growth / 100f));
                            goMockup.transform.localPosition += new Vector3(scale, scale, scale);
                        }
                        else
                        {
                            growerMachine.SetGrowth(100f);
                            if (instance.spawnWorldObjectWhenFullyGrow)
                            {
                                UnityEngine.Object.Destroy(goMockup);
                                machineGrowerInstantiatedGameObject.SetValue(instance, null);

                                var entries = inventory.GetInsideWorldObjects();
                                if (entries.Count != 0) {
                                    entries[0].SetLockInInventoryTime(Time.time + 0.01f);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (growth < 100f)
                        {
                            float scale = instance.growSpeed * updateSizeInterval;
                            goMockup.transform.localPosition += new Vector3(scale, scale, scale);
                            Send(new MessageUpdateGrowth()
                            {
                                machineId = growerMachine.GetId(),
                                growth = growth
                            });
                        }
                        else
                        {
                            growerMachine.SetGrowth(100f);
                            if (instance.spawnWorldObjectWhenFullyGrow)
                            {
                                var seed = inventory.GetInsideWorldObjects()[0];
                                var vegetableGroup = ((GroupItem)seed.GetGroup()).GetGrowableGroup();

                                var vegetableWo = WorldObjectsHandler.CreateNewWorldObject(vegetableGroup);
                                vegetableWo.SetPositionAndRotation(instance.spawnPoint.transform.position, goMockup.transform.rotation);

                                SendWorldObject(vegetableWo, true);
                                Send(new MessageUpdateGrowth()
                                {
                                    machineId = growerMachine.GetId(),
                                    growth = growth,
                                    vegetableId = vegetableWo.GetId()
                                }); ;

                                var vegetableGo = WorldObjectsHandler.InstantiateWorldObject(vegetableWo, false);

                                ActionGrabable ag = vegetableGo.GetComponent<ActionGrabable>();
                                if (ag == null)
                                {
                                    ag = vegetableGo.AddComponent<ActionGrabable>();
                                }
                                ag.grabedEvent = (Grabed)Delegate.Combine(ag.grabedEvent, new Grabed((wo) => MachineGrower_OnVegetableGrabed_Host(instance, wo)));

                                UnityEngine.Object.Destroy(goMockup);
                                machineGrowerInstantiatedGameObject.SetValue(instance, null);

                                var entries = inventory.GetInsideWorldObjects();
                                if (entries.Count != 0)
                                {
                                    entries[0].SetLockInInventoryTime(Time.time + 0.01f);
                                }
                            }
                        }
                    }
                }
                yield return new WaitForSeconds(updateSizeInterval);
            }
        }

        static void MachineGrower_OnVegetableGrabed_Host(MachineGrower machineGrower, WorldObject vegetableWo)
        {
            GameObject goMockup = (GameObject)machineGrowerInstantiatedGameObject.GetValue(machineGrower);
            UnityEngine.Object.Destroy(goMockup);
            machineGrowerInstantiatedGameObject.SetValue(machineGrower, null);

            Inventory inventory = (Inventory)machineGrowerInventory.GetValue(machineGrower);
            WorldObject seedWo = inventory.GetInsideWorldObjects()[0];
            inventory.RemoveItem(seedWo, false);
            if (machineGrower.chanceOfGettingSeedsOnHundred >= (float)UnityEngine.Random.Range(0, 100))
            {
                GetPlayerMainController(); // not sure why
                seedWo.SetLockInInventoryTime(0f);
                inventory.AddItem(seedWo);
                Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer()
                    .AddInformation(2.5f, Readable.GetGroupName(seedWo.GetGroup()), 
                        DataConfig.UiInformationsType.InInventory, seedWo.GetGroup().GetImage());
                return;
            }
            WorldObjectsHandler.DestroyWorldObject(seedWo);
        }

        /// <summary>
        /// The vanilla game calls MachineGrower::HandleAlreadyGrownObject when the game
        /// loads and the machine grower is already at 100% growth. It will despawn the
        /// mockup vegetable and setup an ActionGrabable on the existing vegetable's WorldObject.
        /// Unfortunately, there is no link between the vegetable's WorldObject and this machine grower,
        /// so the vanilla does a full linear search on all world objects and tries to find the one
        /// that is in close proximity to the grower.
        /// 
        /// On the Host, we need to override the ActionGrabable.grabEvent so it goes to
        /// MachineGrower_OnVegetableGrabed_Host.
        /// 
        /// On the client, we do nothing as the server will send down the state information.
        /// </summary>
        /// <param name="___inventory">The inventory to figure out what the vegetable's GroupItem</param>
        /// <returns>True in singleplayer, false in multiplayer.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrower), "HandleAlreadyGrownObject")]
        static bool MachineGrower_HandleAlreadyGrownObject(MachineGrower __instance, Inventory ___inventory, GameObject ___spawnPoint)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                var entries = ___inventory.GetInsideWorldObjects();
                var seedWo = entries[0];
                var vegetableGroup = ((GroupItem)seedWo.GetGroup()).GetGrowableGroup();
                var spawnPointPosition = ___spawnPoint.transform.position;
                float num = 0.2f;
                var vegetableWo = WorldObjectsHandler
                    .GetAllWorldObjects()
                    .AsParallel()
                    .Where(wo => wo.GetGroup() == vegetableGroup && (wo.GetPosition() - spawnPointPosition).magnitude < num)
                    .FirstOrDefault();
                 
                if (vegetableWo != null && TryGetGameObject(vegetableWo, out var vegetableGo))
                {
                    ActionGrabable ag = vegetableGo.GetComponent<ActionGrabable>();
                    if (ag != null)
                    {
                        ag.grabedEvent = (Grabed)Delegate.Combine(ag.grabedEvent, new Grabed((wo) => MachineGrower_OnVegetableGrabed_Host(__instance, wo)));
                    }

                    GameObject goMockup = (GameObject)machineGrowerInstantiatedGameObject.GetValue(__instance);
                    UnityEngine.Object.Destroy(goMockup);
                    machineGrowerInstantiatedGameObject.SetValue(__instance, null);

                    seedWo.SetLockInInventoryTime(Time.time + 0.01f);
                }

                return false;
            }
            else
            if (updateMode == MultiplayerMode.CoopClient)
            {
                return false;
            }
            return true;
        }

        static void ReceiveMessageUpdateGrowth(MessageUpdateGrowth mug)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                if (worldObjectById.TryGetValue(mug.machineId, out var machineWo))
                {
                    machineWo.SetGrowth(mug.growth);
                    if (mug.growth >= 100f)
                    {
                        if (worldObjectById.TryGetValue(mug.vegetableId, out var vegetableWo))
                        {
                            if (TryGetGameObject(vegetableWo, out var vegetableGo))
                            {
                                var ag = vegetableGo.GetComponent<ActionGrabable>();
                                if (ag == null)
                                {
                                    vegetableGo.AddComponent<ActionGrabable>();
                                }
                                if (TryGetGameObject(machineWo, out var machineGo))
                                {
                                    MachineGrower mg = machineGo.GetComponent<MachineGrower>();
                                    if (mg == null)
                                    {
                                        mg = machineGo.GetComponentInChildren<MachineGrower>();
                                    }
                                    if (mg != null)
                                    {
                                        // if the item is grabbed by the client, it sends a grab request,
                                        // which then calls MachineGrower_OnVegetableGrabed_Host on the host,
                                        // that will manipulate the grower inventory and restart the process
                                        // for the client too

                                        // ag.grabedEvent = (Grabed)Delegate.Combine(ag.grabedEvent, new Grabed((wo) => MachineGrower_OnVegetableGrabed_Client(mg, wo)));

                                        GameObject goMockup = (GameObject)machineGrowerInstantiatedGameObject.GetValue(mg);
                                        UnityEngine.Object.Destroy(goMockup);
                                        machineGrowerInstantiatedGameObject.SetValue(mg, null);
                                    }
                                    else
                                    {
                                        LogWarning("ReceiveMessageUpdateGrowth: MachineGrower not found for machine " + mug.machineId);
                                    }
                                }
                                else
                                {
                                    LogWarning("ReceiveMessageUpdateGrowth: GameObject not found for machine " + mug.machineId);
                                }
                            }
                            else
                            {
                                LogWarning("ReceiveMessageUpdateGrowth: GameObject not found for vegetable " + mug.machineId + " id " + mug.vegetableId);
                            }
                        }
                        else
                        {
                            LogWarning("ReceiveMessageUpdateGrowth: Unknown vegetable of machine " + mug.machineId + " id " + mug.vegetableId);
                        }
                    }
                }
                else
                {
                    LogWarning("ReceiveMessageUpdateGrowth: Unknown machine " + mug.machineId);
                }
            }
        }
    }
}
