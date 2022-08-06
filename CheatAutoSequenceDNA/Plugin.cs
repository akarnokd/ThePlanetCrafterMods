using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections;
using System;
using BepInEx.Bootstrap;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System.Linq;

namespace CheatAutoSequenceDNA
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautosequencedna", "(Cheat) Auto Sequence DNA", "1.0.0.0")]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ConfigEntry<bool> incubatorEnabled;

        static ConfigEntry<string> incubatorFertilizerId;

        static ConfigEntry<string> incubatorMutagenId;

        static ConfigEntry<string> incubatorLarvaeId;

        static ConfigEntry<string> incubatorButterflyId;

        static ConfigEntry<string> incubatorBeeId;

        static ConfigEntry<string> incubatorSilkId;

        static ConfigEntry<bool> sequencerEnabled;

        static ConfigEntry<bool> debugMode;

        static Func<string> getMultiplayerMode;

        static Dictionary<WorldObject, GameObject> worldObjectToGameObject;

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            sequencerEnabled = Config.Bind("Sequencer", "Enabled", true, "Should the Tree-sequencer auto sequence?");
            
            incubatorEnabled = Config.Bind("Incubator", "Enabled", true, "Should the Incubator auto sequence?");
            incubatorFertilizerId = Config.Bind("Incubator", "Fertilizer", "*Fertilizer", "The name of the container(s) where to look for fertilizer.");
            incubatorMutagenId = Config.Bind("Incubator", "Mutagen", "*Mutagen", "The name of the container(s) where to look for mutagen.");
            incubatorLarvaeId = Config.Bind("Incubator", "Larvae", "*Larvae", "The name of the container(s) where to look for larvae.");
            incubatorButterflyId = Config.Bind("Incubator", "Butterfly", "*Butterfly", "The name of the container(s) where to deposit the spawned butterflies.");
            incubatorBeeId = Config.Bind("Incubator", "Bee", "*Bee", "The name of the container(s) where to deposit the spawned bees.");
            incubatorSilkId = Config.Bind("Incubator", "Silk", "*Silk", "The name of the container(s) where to deposit the spawned silk worms.");


            debugMode = Config.Bind("General", "DebugMode", false, "Enable debugging with detailed logs (chatty!).");

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                Logger.LogInfo("Mod " + modFeatMultiplayerGuid + " found, managing multiplayer mode");

                getMultiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);

            }

            worldObjectToGameObject = (Dictionary<WorldObject, GameObject>)AccessTools.Field(typeof(WorldObjectsHandler), "worldObjects").GetValue(null);

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));

            StartCoroutine(SequencerCheckLoop(2.5f));
        }

        static void log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }
        IEnumerator SequencerCheckLoop(float delay)
        {
            for (; ; )
            {
                PlayersManager playersManager = Managers.GetManager<PlayersManager>();
                if (playersManager != null)
                {
                    PlayerMainController player = playersManager.GetActivePlayerController();
                    if (player != null)
                    {
                        try
                        {
                            if (getMultiplayerMode == null || getMultiplayerMode() != "CoopClient")
                            {
                                if (incubatorEnabled.Value)
                                {
                                    HandleIncubators();
                                }
                                if (sequencerEnabled.Value)
                                {
                                    HandleSequencers();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex);
                        }
                    }
                }
                
                yield return new WaitForSeconds(delay);
            }
        }

        string DebugWorldObject(WorldObject wo)
        {
            var str = wo.GetId() + ", " + wo.GetGroup().GetId();
            var txt = wo.GetText();
            if (!string.IsNullOrEmpty(txt))
            {
                str += ", \"" + txt + "\"";
            }
            str += ", " + (wo.GetIsPlaced() ? wo.GetPosition() : "");
            return str;
        }

        List<WorldObject> GetOrCreate(Dictionary<string, List<WorldObject>> list, string key)
        {
            if (list.TryGetValue(key, out var result))
            {
                return result;
            }
            result = new();
            list[key] = result;
            return result;
        }

        void HandleIncubators()
        {
            log("Begin");
            // Category keys for various source and target container names
            Dictionary<string, string> keywordMapping = new()
            {
                { "Fertilizer", incubatorFertilizerId.Value },
                { "Mutagen", incubatorMutagenId.Value },
                { "Larvae", incubatorLarvaeId.Value },
                { "Butterfly", incubatorButterflyId.Value },
                { "Bee", incubatorBeeId.Value },
                { "Silk", incubatorSilkId.Value }
            };

            // List of world objects per category (containers, machines)
            Dictionary<string, List<WorldObject>> itemCategories = new();

            log("  Container discovery");
            foreach (WorldObject wo in WorldObjectsHandler.GetConstructedWorldObjects())
            {
                var gid = wo.GetGroup().GetId();
                var txt = wo.GetText() ?? "";
                if (gid == "Container1" || gid == "Container2")
                {
                    foreach (var kv in keywordMapping)
                    {
                        if (txt.Contains(kv.Value))
                        {
                            GetOrCreate(itemCategories, kv.Key).Add(wo);
                            log("    " + kv.Key + " <- " + DebugWorldObject(wo));
                        }
                    }
                }
                if (gid == "Incubator1")
                {
                    GetOrCreate(itemCategories, "Incubator").Add(wo);
                    log("    Incubator <- " + DebugWorldObject(wo));
                }
            }

            if (itemCategories.TryGetValue("Incubator", out var incubatorList))
            {
                foreach (var incubator in incubatorList)
                {
                    log("  Incubator: " + DebugWorldObject(incubator));
                    // Try to deposit finished products first
                    Inventory incubatorInv = InventoriesHandler.GetInventoryById(incubator.GetLinkedInventoryId());

                    log("    Depositing products");
                    var currentItems = incubatorInv.GetInsideWorldObjects();
                    List<WorldObject> items = new(currentItems);
                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        WorldObject item = items[i];
                        var gid = item.GetGroup().GetId();
                        if (gid.StartsWith("Butterfly"))
                        {
                            TryDeposit(incubatorInv, item, itemCategories, "Butterfly");
                        }
                        if (gid.StartsWith("Bee"))
                        {
                            TryDeposit(incubatorInv, item, itemCategories, "Bee");
                        }
                        if (gid.StartsWith("Silk"))
                        {
                            TryDeposit(incubatorInv, item, itemCategories, "Silk");
                        }
                    }

                    if (incubator.GetGrowth() == 0)
                    {
                        log("    Collecting ingredients");
                        bool hasLarvae = false;
                        bool hasMutagen = false;
                        bool hasFertilizer = false;

                        foreach (var wo in currentItems)
                        {
                            var gid = wo.GetGroup().GetId();
                            if (gid.StartsWith("LarvaeBase"))
                            {
                                hasLarvae = true;
                            }
                            hasMutagen |= gid == "Mutagen1";
                            hasFertilizer |= gid == "Fertilizer1";
                        }

                        if (!hasLarvae)
                        {
                            hasLarvae = TryCollect(incubatorInv, "LarvaeBase", itemCategories, "Larvae");
                        }
                        if (!hasMutagen)
                        {
                            hasMutagen = TryCollect(incubatorInv, "Mutagen1", itemCategories, "Mutagen");
                        }
                        if (!hasFertilizer)
                        {
                            hasFertilizer = TryCollect(incubatorInv, "Fertilizer1", itemCategories, "Fertilizer");
                        }

                        if (hasLarvae && hasMutagen && hasFertilizer)
                        {
                            var spawnTarget = Analyze(currentItems, DataConfig.CraftableIn.CraftInsectsT1);
                            log("    Sequencing: " + spawnTarget.GetId() + " (" + spawnTarget.GetChanceToSpawn() * 100 + " %)");

                            incubator.SetGrowth(1f);
                            incubator.SetLinkedGroups(new List<Group> { spawnTarget });

                            var t = Time.time + 0.01f;
                            foreach (WorldObject wo in incubatorInv.GetInsideWorldObjects())
                            {
                                wo.SetLockInInventoryTime(t);
                            }
                            StartCoroutine(RefreshDisplayer(0.1f, incubatorInv));
                        }
                        else
                        {
                            if (!hasLarvae)
                            {
                                log("      Missing Larvae ingredient");
                            }
                            if (!hasMutagen)
                            {
                                log("      Missing Mutagen ingredient");
                            }
                            if (!hasFertilizer)
                            {
                                log("      Missing Fertilizer ingredient");
                            }
                        }
                    }
                }
            }
            log("Done");
        }

        GroupItem Analyze(List<WorldObject> currentItems, DataConfig.CraftableIn craftableIn)
        {
            List<Group> inputGroups = new();
            foreach (var wo in currentItems)
            {
                inputGroups.Add(wo.GetGroup());
            }

            List<GroupItem> candidates = new();

            foreach (var gi in GroupsHandler.GetGroupsItem())
            {
                if (gi.CanBeCraftedIn(craftableIn) && gi.GetUnlockingInfos().GetIsUnlocked())
                {
                    var recipe = gi.GetRecipe().GetIngredientsGroupInRecipe();
                    
                    if (inputGroups.Count == recipe.Count && !recipe.Except(inputGroups).Any())
                    {
                        candidates.Add(gi);
                    }
                }
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }
            if (candidates.Count > 1)
            {
                var set = new List<GroupItem>(candidates);

                var max = 200;
                while (max-- > 0)
                {
                    int index = UnityEngine.Random.Range(0, set.Count);
                    var candidate = set[index];

                    if (candidate.GetChanceToSpawn() == 0f 
                        || candidate.GetChanceToSpawn() >= UnityEngine.Random.Range(0, 100))
                    {
                        return candidate;
                    }

                    set.RemoveAt(index);
                    if (set.Count == 0)
                    {
                        set = new List<GroupItem>(candidates);
                    }
                }
            }
            return null;
        }

        IEnumerator RefreshDisplayer(float t, Inventory inv)
        {
            yield return new WaitForSeconds(t);
            inv.RefreshDisplayerContent();
        }

        void TryDeposit(Inventory source, WorldObject item, 
            Dictionary<string, List<WorldObject>> itemCategories, string itemKey)
        {
            log("      Deposit item: " + DebugWorldObject(item));
            if (itemCategories.TryGetValue(itemKey, out var containers))
            {
                foreach (var container in containers)
                {
                    Inventory inv = InventoriesHandler.GetInventoryById(container.GetLinkedInventoryId());
                    if (inv.AddItem(item))
                    {
                        log("        Into      : " + DebugWorldObject(container));
                        source.RemoveItem(item, false);
                        return;
                    }
                }
                log("        Into      : Failed - all target containers are full");
                return;
            }
            log("        Into      : Failed - no target containers found");
        }

        bool TryCollect(Inventory destination, string gid, 
            Dictionary<string, List<WorldObject>> itemCategories, string itemKey)
        {
            if (itemCategories.TryGetValue(itemKey, out var containers))
            {
                foreach (var container in containers)
                {
                    Inventory inv = InventoriesHandler.GetInventoryById(container.GetLinkedInventoryId());

                    foreach (var wo in inv.GetInsideWorldObjects())
                    {
                        var woGid = wo.GetGroup().GetId();
                        if (woGid.StartsWith(gid))
                        {
                            if (destination.AddItem(wo))
                            {
                                log("      Collect item: " + DebugWorldObject(wo));
                                log("        From      : " + DebugWorldObject(container));
                                inv.RemoveItem(wo, false);
                                return true;
                            }
                        }
                    }
                }
                log("      Collect item: Failed - no items found");
                return false;
            }
            log("      Collect item: Failed - no containers found");
            return false;
        }

        void HandleSequencers()
        {
            
        }
    }
}
