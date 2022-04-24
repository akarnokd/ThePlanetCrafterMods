using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using System;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Bootstrap;
using System.Diagnostics;

namespace CheatAutoHarvest
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautoharvest", "(Cheat) Automatically Harvest Food n Algae", "1.0.0.2")]
    [BepInDependency(cheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string cheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";

        static MethodInfo updateGrowing;
        static MethodInfo instantiateAtRandomPosition;
        static FieldInfo machineGrowerInventory;
        static FieldInfo worldObjectsDictionary;

        static ManualLogSource logger;
        static bool debugAlgae = false;
        static bool debugFood = false;

        static ConfigEntry<bool> harvestAlgae;
        static ConfigEntry<bool> harvestFood;

        static Func<List<WorldObject>, int, string, bool> isFullStacked;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;
            updateGrowing = AccessTools.Method(typeof(MachineOutsideGrower), "UpdateGrowing", new Type[] { typeof(float) });
            instantiateAtRandomPosition = AccessTools.Method(typeof(MachineOutsideGrower), "InstantiateAtRandomPosition", new Type[] { typeof(GameObject), typeof(bool) });
            machineGrowerInventory = AccessTools.Field(typeof(MachineGrower), "inventory");
            worldObjectsDictionary = AccessTools.Field(typeof(WorldObjectsHandler), "worldObjects");
            harvestAlgae = Config.Bind("General", "HarvestAlgae", true, "Enable auto harvesting for algae.");
            harvestFood = Config.Bind("General", "HarvestFood", true, "Enable auto harvesting for food.");

            if (Chainloader.PluginInfos.TryGetValue(cheatInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "IsFullStacked", new Type[] { typeof(List<WorldObject>), typeof(int), typeof(string) });
                isFullStacked = AccessTools.MethodDelegate<Func<List<WorldObject>, int, string, bool>>(mi, pi.Instance);
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void logAlgae(string s)
        {
            if (debugAlgae)
            {
                logger.LogInfo(s);
            }
        }
        static void logFood(string s)
        {
            if (debugFood)
            {
                logger.LogInfo(s);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MachineOutsideGrower), "Grow")]
        static void MachineOutsideGrower_Grow(
            MachineOutsideGrower __instance, 
            float ___growSize,
            WorldObject ___worldObjectGrower, 
            List<GameObject> ___instantiatedGameObjects,
            float ___updateInterval,
            int ___spawNumber)
        {
            if (!harvestAlgae.Value)
            {
                return;
            }

            if (___instantiatedGameObjects != null)
            {
                bool restartCoroutine = false;

                logAlgae("Grower: " + ___worldObjectGrower.GetId() + " @ " + ___worldObjectGrower.GetGrowth() + " - " + ___instantiatedGameObjects.Count + " < " + ___spawNumber);
                foreach (GameObject go in new List<GameObject>(___instantiatedGameObjects))
                {
                    if (go != null)
                    {
                        ActionGrabable ag = go.GetComponent<ActionGrabable>();
                        if (ag != null)
                        {
                            WorldObjectAssociated woa = go.GetComponent<WorldObjectAssociated>();
                            if (woa != null)
                            {
                                WorldObject wo = woa.GetWorldObject();
                                if (wo != null)
                                {
                                    float progress = 100f * go.transform.localScale.x / ___growSize;
                                    logAlgae("  - [" + wo.GetId() + "]  "  + wo.GetGroup().GetId() + " @ " + (progress) + "%");
                                    if (progress >= 100f)
                                    {
                                        if (FindInventory(wo, out Inventory inv))
                                        {
                                            if (inv.AddItem(wo))
                                            {
                                                logAlgae("    Deposited [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                                                wo.SetDontSaveMe(false);

                                                ___instantiatedGameObjects.Remove(go);
                                                UnityEngine.Object.Destroy(go);

                                                // from OnGrabedAGrowing to avoid reentrance

                                                GroupItem growableGroup = ((GroupItem)wo.GetGroup()).GetGrowableGroup();
                                                GameObject objectToInstantiate = (growableGroup != null) ? growableGroup.GetAssociatedGameObject() : wo.GetGroup().GetAssociatedGameObject();
                                                instantiateAtRandomPosition.Invoke(__instance, new object[] { objectToInstantiate, false });

                                                restartCoroutine = true;
                                            }
                                            else
                                            {
                                                logAlgae("    Inventory full [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                                            }
                                        }
                                        else
                                        {
                                            logAlgae("    No inventory for [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                logAlgae("Grower: " + ___worldObjectGrower.GetId() + " @ " + ___worldObjectGrower.GetGrowth() + " - " + ___instantiatedGameObjects.Count + " ---- DONE");

                if (restartCoroutine)
                {
                    __instance.StopAllCoroutines();
                    __instance.StartCoroutine((IEnumerator)updateGrowing.Invoke(__instance, new object[] { ___updateInterval }));
                }
            }
        }

        void Start()
        {
            StartCoroutine(CheckFoodGrowersLoop(5));
        }

        IEnumerator CheckFoodGrowersLoop(float delay)
        {
            for (; ; )
            {
                var t = Stopwatch.GetTimestamp();
                CheckFoodGrowers();
                logFood("Perf: " + (Stopwatch.GetTimestamp() - t) / 10000f);
                yield return new WaitForSeconds(delay);
            }
        }

        void CheckFoodGrowers()
        {
            if (!harvestFood.Value)
            {
                return;
            }
            logFood("Edible: Ingame?");
            if (Managers.GetManager<PlayersManager>() == null)
            {
                return;
            }

            logFood("Edible: begin search");
            int deposited = 0;
            Dictionary<WorldObject, GameObject> map = (Dictionary<WorldObject, GameObject>)worldObjectsDictionary.GetValue(null);

            List<MachineGrower> allMachineGrowers = new List<MachineGrower>();
            List<WorldObject> food = new List<WorldObject>();
            List<InventoryAndWorldObject> inventories = new List<InventoryAndWorldObject>();

            FindObjects(map, food, inventories, allMachineGrowers);
            logFood("  Enumerated food: " + food.Count);
            logFood("  Enumerated inventories: " + inventories.Count);
            logFood("  Enumerated machine growers: " + allMachineGrowers.Count);

            foreach (WorldObject wo in food)
            {
                Group g = wo.GetGroup();
                logFood("Edible for grab: " + wo.GetId() + " of *" + g.id);
                if (FindInventory(wo, inventories, out Inventory inv))
                {
                    logFood("  Found inventory.");

                    bool found = false;
                    // we have to find which grower wo came from so it can be reset
                    foreach (MachineGrower mg in allMachineGrowers)
                    {
                        if ((wo.GetPosition() - mg.spawnPoint.transform.position).magnitude < 0.2f)
                        {
                            found = true;
                            logFood("  Found MachineGrower");
                            if (inv.AddItem(wo))
                            {
                                logFood("  Adding to target inventory");
                                if (map.TryGetValue(wo, out GameObject go) && go != null)
                                {
                                    UnityEngine.Object.Destroy(go);
                                }

                                // readd seed
                                Inventory machineInventory = (Inventory)machineGrowerInventory.GetValue(mg);

                                WorldObject seed = machineInventory.GetInsideWorldObjects()[0];

                                machineInventory.RemoveItem(seed, false);
                                seed.SetLockInInventoryTime(0f);
                                machineInventory.AddItem(seed);

                                deposited++;
                            }
                            else
                            {
                                logAlgae("    Inventory full [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                            }

                            break;
                        }
                    }
                    if (!found)
                    {
                        logFood("  Could not find MachineGrower of this edible");
                    }
                }
            }
            logFood("Edible deposited: " + deposited);
        }

        static bool IsFull(Inventory inv, WorldObject wo)
        {
            if (isFullStacked != null)
            {
                return isFullStacked.Invoke(inv.GetInsideWorldObjects(), inv.GetSize(), wo.GetGroup().GetId());
            }
            return inv.IsFull();
        }

        class InventoryAndWorldObject
        {
            internal Inventory inventory;
            internal WorldObject worldObject;
        }

        static void FindObjects(Dictionary<WorldObject, GameObject> map, 
            List<WorldObject> food, 
            List<InventoryAndWorldObject> inventories, 
            List<MachineGrower> growers)
        {
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                GroupItem g = wo.GetGroup() as GroupItem;
                if (g != null && g.GetUsableType() == DataConfig.UsableType.Eatable)
                {
                    if (map.TryGetValue(wo, out GameObject go) && go != null)
                    {
                        ActionGrabable ag = go.GetComponent<ActionGrabable>();
                        if (ag != null)
                        {
                            food.Add(wo);
                        }
                    }
                }
                string txt = wo.GetText();
                if (txt != null && txt.Contains("*"))
                {
                    if (wo.HasLinkedInventory())
                    {
                        Inventory inv = InventoriesHandler.GetInventoryById(wo.GetLinkedInventoryId());
                        if (inv != null)
                        {
                            InventoryAndWorldObject iwo = new InventoryAndWorldObject();
                            iwo.inventory = inv;
                            iwo.worldObject = wo;
                            inventories.Add(iwo);
                        }
                    }
                }
                if (map.TryGetValue(wo, out GameObject goConstr) && goConstr != null)
                {
                    MachineGrower goMg = goConstr.GetComponent<MachineGrower>();
                    if (goMg != null)
                    {
                        growers.Add(goMg);
                    }
                }
            }
        }

        static bool FindInventory(WorldObject wo, List<InventoryAndWorldObject> inventories, out Inventory inventory)
        {
            string gid = "*" + wo.GetGroup().GetId().ToLower();
            foreach (InventoryAndWorldObject inv in inventories)
            {
                string txt = inv.worldObject.GetText();
                if (txt != null && txt.ToLower().Contains(gid))
                {
                    if (!IsFull(inv.inventory, wo))
                    {
                        inventory = inv.inventory;
                        return true;
                    }
                }
            }
            inventory = null;
            return false;
        }

        static bool FindInventory(WorldObject wo, out Inventory inventory)
        {
            string gid = "*" + wo.GetGroup().GetId().ToLower();
            //logger.LogInfo("    Finding inventory for " + gid);
            foreach (WorldObject wo2 in WorldObjectsHandler.GetAllWorldObjects())
            {
                if (wo2 != null && wo2.HasLinkedInventory())
                {
                    Inventory inv2 = InventoriesHandler.GetInventoryById(wo2.GetLinkedInventoryId());
                    if (inv2 != null && !IsFull(inv2, wo))
                    {
                        string txt = wo2.GetText();
                        if (txt != null && txt.ToLower().Contains(gid))
                        {
                            inventory = inv2;
                            return true;
                        }
                    }
                }
            }
            inventory = null;
            return false;
        }
    }
}
