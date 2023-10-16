using BepInEx;
using SpaceCraft;
using HarmonyLib;
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
    [BepInPlugin(modCheatAutoHarvest, "(Cheat) Automatically Harvest Food n Algae", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modCheatInventoryStackingGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatInventoryStackingGuid = "akarnokd.theplanetcraftermods.cheatinventorystacking";
        const string modCheatAutoHarvest = "akarnokd.theplanetcraftermods.cheatautoharvest";

        static MethodInfo updateGrowing;
        static MethodInfo instantiateAtRandomPosition;
        static FieldInfo machineGrowerInventory;

        static ManualLogSource logger;
        static ConfigEntry<bool> debugAlgae;
        static ConfigEntry<bool> debugFood;

        static ConfigEntry<bool> harvestAlgae;
        static ConfigEntry<bool> harvestFood;

        static readonly Dictionary<string, ConfigEntry<string>> depositAliases = new();

        static Func<List<WorldObject>, int, string, bool> isFullStacked;

        static bool loadCompleted;

        /// <summary>
        /// Set this callback and make it return false to prevent this mod from working.
        /// </summary>
        public static Func<bool> canExecute;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;
            updateGrowing = AccessTools.Method(typeof(MachineOutsideGrower), "UpdateGrowing", new Type[] { typeof(float) });
            instantiateAtRandomPosition = AccessTools.Method(typeof(MachineOutsideGrower), "InstantiateAtRandomPosition", new Type[] { typeof(GameObject), typeof(bool) });
            machineGrowerInventory = AccessTools.Field(typeof(MachineGrower), "inventory");
            harvestAlgae = Config.Bind("General", "HarvestAlgae", true, "Enable auto harvesting for algae.");
            harvestFood = Config.Bind("General", "HarvestFood", true, "Enable auto harvesting for food.");
            debugAlgae = Config.Bind("General", "DebugAlgae", false, "Enable debug log for algae (chatty!)");
            debugFood = Config.Bind("General", "DebugFood", false, "Enable debug log for food (chatty!)");

            depositAliases["Algae1Seed"] = Config.Bind("General", "AliasAlgae", "*Algae1Seed", "The container name to put algae into.");
            depositAliases["Vegetable0Growable"] = Config.Bind("General", "AliasEggplant", "*Vegetable0Growable", "The container name to put eggplant into.");
            depositAliases["Vegetable1Growable"] = Config.Bind("General", "AliasSquash", "*Vegetable1Growable", "The container name to put squash into.");
            depositAliases["Vegetable2Growable"] = Config.Bind("General", "AliasBeans", "*Vegetable2Growable", "The container name to put beans into.");
            depositAliases["Vegetable3Growable"] = Config.Bind("General", "AliasMushroom", "*Vegetable3Growable", "The container name to put mushroom into.");
            depositAliases["CookCocoaGrowable"] = Config.Bind("General", "AliasCocoa", "*CookCocoaGrowable", "The container name to put cocoa into.");
            depositAliases["CookWheatGrowable"] = Config.Bind("General", "AliasWheat", "*CookWheatGrowable", "The container name to put wheat into.");

            if (debugAlgae.Value || debugFood.Value)
            {
                foreach (var kv in depositAliases) 
                {
                    Logger.LogInfo("  Alias " + kv.Key + " -> " + kv.Value.Value);
                }
            }

            if (Chainloader.PluginInfos.TryGetValue(modCheatInventoryStackingGuid, out BepInEx.PluginInfo pi))
            {
                MethodInfo mi = AccessTools.Method(pi.Instance.GetType(), "IsFullStacked", new Type[] { typeof(List<WorldObject>), typeof(int), typeof(string) });
                isFullStacked = AccessTools.MethodDelegate<Func<List<WorldObject>, int, string, bool>>(mi, pi.Instance);
            }

            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.SaveModInfo.Patch(harmony);
        }

        static void LogAlgae(string s)
        {
            if (debugAlgae.Value)
            {
                logger.LogInfo(s);
            }
        }
        static void LogFood(string s)
        {
            if (debugFood.Value)
            {
                logger.LogInfo(s);
            }
        }

        static List<InventoryAndWorldObject> inventoriesCache;
        static int inventoriesCacheFrame;

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
            if (!harvestAlgae.Value || (canExecute != null && !canExecute.Invoke()))
            {
                return;
            }
            if (!loadCompleted)
            {
                LogAlgae("Algae: Game is still loading.");
                inventoriesCache = null;
                inventoriesCacheFrame = 0;
                return;
            }
            LogAlgae("Algae: Ingame?");
            if (Managers.GetManager<PlayersManager>() == null)
            {
                return;
            }
            if (___instantiatedGameObjects != null)
            {
                var now = new Stopwatch();
                now.Start();

                bool restartCoroutine = false;

                LogAlgae("Grower: " + ___worldObjectGrower.GetId() + " @ " + ___worldObjectGrower.GetGrowth() + " - " + ___instantiatedGameObjects.Count + " < " + ___spawNumber);

                List<InventoryAndWorldObject> inventories = inventoriesCache;
                int frame = inventoriesCacheFrame;
                int currentFrame = Time.frameCount;
                if (inventories == null || frame != currentFrame)
                {
                    LogAlgae("  Grower lookup all inventories this frame.");
                    inventories = new List<InventoryAndWorldObject>();
                    FindInventories(inventories);
                    
                    inventoriesCache = inventories;
                    inventoriesCacheFrame = currentFrame;
                }

                LogAlgae("  Grower Inventory lookup time: " + (now.ElapsedTicks / 10000f) + " ms");
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
                                    LogAlgae("  - [" + wo.GetId() + "]  "  + wo.GetGroup().GetId() + " @ " + (progress) + "%");
                                    if (progress >= 100f)
                                    {
                                        if (FindInventory(wo, inventories, out Inventory inv))
                                        {
                                            if (inv.AddItem(wo))
                                            {
                                                LogAlgae("    Deposited [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                                                wo.SetDontSaveMe(false);

                                                ___instantiatedGameObjects.Remove(go);
                                                ag.grabedEvent = null; // prevent respawn duplication due to ActionGrabable::OnDestroy
                                                UnityEngine.Object.Destroy(go);

                                                // from OnGrabedAGrowing to avoid reentrance

                                                GroupItem growableGroup = ((GroupItem)wo.GetGroup()).GetGrowableGroup();
                                                GameObject objectToInstantiate = (growableGroup != null) ? growableGroup.GetAssociatedGameObject() : wo.GetGroup().GetAssociatedGameObject();
                                                instantiateAtRandomPosition.Invoke(__instance, new object[] { objectToInstantiate, false });

                                                restartCoroutine = true;
                                            }
                                            else
                                            {
                                                LogAlgae("    Inventory full [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                                            }
                                        }
                                        else
                                        {
                                            LogAlgae("    No inventory for [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                LogAlgae("Grower: " + ___worldObjectGrower.GetId() + " @ " + ___worldObjectGrower.GetGrowth() + " - " + ___instantiatedGameObjects.Count + " ---- DONE in " + (now.ElapsedTicks / 10000f) + " ms");

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
                LogFood("Perf: " + (Stopwatch.GetTimestamp() - t) / 10000f);
                yield return new WaitForSeconds(delay);
            }
        }

        void CheckFoodGrowers()
        {
            if (!harvestFood.Value || (canExecute != null && !canExecute.Invoke()))
            {
                return;
            }
            if (!loadCompleted)
            {
                LogFood("Algae: Game is still loading.");
                return;
            }
            LogFood("Edible: Ingame?");
            if (Managers.GetManager<PlayersManager>() == null)
            {
                return;
            }

            LogFood("Edible: begin search");
            var now = new Stopwatch();
            now.Start();
            int deposited = 0;

            List<MachineGrower> allMachineGrowers = new();
            List<WorldObject> food = new();
            List<InventoryAndWorldObject> inventories = new();

            FindObjects(food, inventories, allMachineGrowers);
            LogFood("  Enumerated food: " + food.Count);
            LogFood("  Enumerated inventories: " + inventories.Count);
            LogFood("  Enumerated machine growers: " + allMachineGrowers.Count);

            foreach (WorldObject wo in food)
            {
                Group g = wo.GetGroup();
                LogFood("Edible for grab: " + wo.GetId() + " of *" + g.id);
                if (FindInventory(wo, inventories, out Inventory inv))
                {
                    LogFood("  Found inventory.");

                    bool found = false;
                    // we have to find which grower wo came from so it can be reset
                    foreach (MachineGrower mg in allMachineGrowers)
                    {
                        if ((wo.GetPosition() - mg.spawnPoint.transform.position).magnitude < 0.2f)
                        {
                            found = true;
                            LogFood("  Found MachineGrower");
                            if (inv.AddItem(wo))
                            {
                                LogFood("  Adding to target inventory");
                                var go = wo.GetGameObject();
                                if (go != null)
                                {
                                    Destroy(go);
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
                                LogAlgae("    Inventory full [" + wo.GetId() + "]  *" + wo.GetGroup().GetId());
                            }

                            break;
                        }
                    }
                    if (!found)
                    {
                        LogFood("  Could not find MachineGrower of this edible");
                    }
                }
            }
            LogFood("Edible deposited: " + deposited + " in " + (now.ElapsedTicks / 10000f) + " ms");
        }

        static bool IsFull(Inventory inv, WorldObject wo)
        {
            if (isFullStacked != null)
            {
                // fine, machines always stack for now
                return isFullStacked.Invoke(inv.GetInsideWorldObjects(), inv.GetSize(), wo.GetGroup().GetId());
            }
            return inv.IsFull();
        }

        class InventoryAndWorldObject
        {
            internal Inventory inventory;
            internal WorldObject worldObject;
        }

        static void FindInventories(List<InventoryAndWorldObject> inventories)
        {
            foreach (WorldObject wo in WorldObjectsHandler.GetConstructedWorldObjects())
            {
                string txt = wo.GetText();
                if (txt != null && IsTargetAlias(txt))
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
            }
        }

        static bool IsTargetAlias(string txt)
        {
            txt = txt.ToLower();
            foreach (var kv in depositAliases)
            {
                if (txt.Contains(kv.Value.Value.ToLower()))
                {
                    return true;
                }
            }
            return false;
        }

        static void FindObjects(
            List<WorldObject> food, 
            List<InventoryAndWorldObject> inventories, 
            List<MachineGrower> growers)
        {
            foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
            {
                GroupItem g = wo.GetGroup() as GroupItem;
                if (g != null && g.GetUsableType() == DataConfig.UsableType.Eatable)
                {
                    GameObject go = wo.GetGameObject();
                    if (go != null)
                    {
                        ActionGrabable ag = go.GetComponent<ActionGrabable>();
                        if (ag != null)
                        {
                            food.Add(wo);
                        }
                    }
                }
                string txt = wo.GetText();
                if (txt != null && IsTargetAlias(txt))
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
                var goConstr = wo.GetGameObject();
                if (goConstr != null)
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
            var gr = wo.GetGroup().GetId();
            if (debugAlgae.Value || debugFood.Value)
            {
                logger.LogInfo("  Checking alias for " + gr);
            }
            if (depositAliases.TryGetValue(gr, out var gid))
            {
                if (debugAlgae.Value || debugFood.Value)
                {
                    logger.LogInfo("  Looking for inventory containing alias " + gid.Value);
                }
                var gidv = gid.Value.ToLower();
                foreach (InventoryAndWorldObject inv in inventories)
                {
                    string txt = inv.worldObject.GetText();
                    if (txt != null && txt.ToLower().Contains(gidv))
                    {
                        if (!IsFull(inv.inventory, wo))
                        {
                            inventory = inv.inventory;
                            return true;
                        }
                    }
                }
            }
            if (debugAlgae.Value || debugFood.Value)
            {
                logger.LogInfo("  No inventory for " + wo.GetGroup().GetId());
            }
            inventory = null;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            loadCompleted = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            loadCompleted = false;
        }
    }
}
