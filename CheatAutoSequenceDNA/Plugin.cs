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
using static SpaceCraft.DataConfig;

namespace CheatAutoSequenceDNA
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautosequencedna", "(Cheat) Auto Sequence DNA", "1.0.0.2")]
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

        static ConfigEntry<string> sequencerMutagenId;

        static ConfigEntry<string> sequencerTreeRootId;

        static ConfigEntry<string> sequencerFlowerSeedId;

        static ConfigEntry<string> sequencerTreeSeedId;

        static ConfigEntry<bool> debugMode;

        static Func<string> getMultiplayerMode;

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            sequencerEnabled = Config.Bind("Sequencer", "Enabled", true, "Should the Tree-sequencer auto sequence?");
            
            incubatorEnabled = Config.Bind("Incubator", "Enabled", true, "Should the Incubator auto sequence?");
            incubatorFertilizerId = Config.Bind("Incubator", "Fertilizer", "*Fertilizer", "The name of the container(s) where to look for fertilizer.");
            incubatorMutagenId = Config.Bind("Incubator", "Mutagen", "*Mutagen", "The name of the container(s) where to look for mutagen.");
            incubatorLarvaeId = Config.Bind("Incubator", "Larvae", "*Larvae", "The name of the container(s) where to look for larvae (common, uncommon, rare).");
            incubatorButterflyId = Config.Bind("Incubator", "Butterfly", "*Butterfly", "The name of the container(s) where to deposit the spawned butterflies.");
            incubatorBeeId = Config.Bind("Incubator", "Bee", "*Bee", "The name of the container(s) where to deposit the spawned bees.");
            incubatorSilkId = Config.Bind("Incubator", "Silk", "*Silk", "The name of the container(s) where to deposit the spawned silk worms.");

            sequencerMutagenId = Config.Bind("Sequencer", "Mutagen", "*Mutagen", "The name of the container(s) where to look for fertilizer.");
            sequencerTreeRootId = Config.Bind("Sequencer", "TreeRoot", "*TreeRoot", "The name of the container(s) where to look for Tree Root.");
            sequencerFlowerSeedId = Config.Bind("Sequencer", "FlowerSeed", "*FlowerSeed", "The name of the container(s) where to look for Flower Seeds (all kinds).");
            sequencerTreeSeedId = Config.Bind("Sequencer", "TreeSeed", "*TreeSeed", "The name of the container(s) where to deposit the spawned tree seeds.");

            debugMode = Config.Bind("General", "DebugMode", false, "Enable debugging with detailed logs (chatty!).");

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                Logger.LogInfo("Mod " + modFeatMultiplayerGuid + " found, managing multiplayer mode");

                getMultiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);

            }

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
            log("Begin<Incubators>");
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

                    var currentItems = incubatorInv.GetInsideWorldObjects();
                    if (currentItems.Count > 0 && incubator.GetGrowth() == 0)
                    {
                        log("    Depositing products");
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
                    }

                    if (incubator.GetGrowth() == 0)
                    {
                        log("    Picking Recipe");

                        var spawnTarget = PickRecipe(DataConfig.CraftableIn.CraftInsectsT1);

                        if (spawnTarget != null)
                        {
                            log("    Picked: " + spawnTarget.id + " (\"" + Readable.GetGroupName(spawnTarget) + "\") @ Chance = " + spawnTarget.GetChanceToSpawn() + " %");

                            List<Group> ingredients = new(spawnTarget.GetRecipe().GetIngredientsGroupInRecipe());
                            List<WorldObject> available = new(currentItems);
                            List<Group> missing = new();

                            int ingredientsFulfilled = 0;

                            log("    Checking inventory for ingredients");
                            // check each ingredient
                            foreach (var ingredient in ingredients)
                            {
                                var igid = ingredient.GetId();
                                bool found = false;
                                // do we have it already in the inventory
                                for (int i = 0; i < available.Count; i++)
                                {
                                    WorldObject curr = available[i];
                                    if (curr != null && curr.GetGroup().GetId() == igid)
                                    {
                                        available[i] = null;
                                        found = true;
                                        break;
                                    }
                                }
                                if (!found)
                                {
                                    log("      Not in inventory: " + ingredient.id);
                                    missing.Add(ingredient);
                                }
                                else
                                {
                                    ingredientsFulfilled++;
                                    log("      Found in inventory: " + ingredient.id);
                                }
                            }

                            List<IngredientSource> toTransfer = new();

                            if (missing.Count != 0)
                            {
                                log("    Checking containers for missing ingredients");
                                Dictionary<string, List<IngredientSource>> sourcesPerCategory = new();

                                foreach (var m in missing)
                                {
                                    var cat = GetCategoryFor(m.id);
                                    if (!sourcesPerCategory.TryGetValue(cat, out var sources))
                                    {
                                        List<IngredientSource> ingredientSources = new();
                                        FindIngredientsIn(m.id, itemCategories, cat, ingredientSources);

                                        sourcesPerCategory[cat] = ingredientSources;
                                    }
                                }

                                foreach (var m in missing)
                                {
                                    var cat = GetCategoryFor(m.id);
                                    var sources = sourcesPerCategory[cat];
                                    bool found = false;
                                    for (int i = 0; i < sources.Count; i++)
                                    {
                                        var source = sources[i];
                                        if (source != null && source.ingredient == m.id)
                                        {
                                            sources[i] = null;
                                            toTransfer.Add(source);
                                            ingredientsFulfilled++;
                                            found = true;
                                            log("      Ingredient Found " + m.id);
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        log("      No source for ingredient " + m.id);
                                    }
                                }
                            }

                            log("    Recipe check: Found = " + ingredientsFulfilled + ", Required = " + ingredients.Count);
                            if (ingredientsFulfilled == ingredients.Count)
                            {
                                bool transferSuccess = true;
                                if (toTransfer.Count != 0)
                                {
                                    log("      Transferring ingredients");
                                    foreach (var tt in toTransfer)
                                    {
                                        if (incubatorInv.AddItem(tt.wo))
                                        {
                                            log("        From " + tt.source.GetId() + ": " + DebugWorldObject(tt.wo) + " SUCCESS");
                                            tt.source.RemoveItem(tt.wo);
                                        }
                                        else
                                        {
                                            log("        From " + tt.source.GetId() + ": " + DebugWorldObject(tt.wo) + " FAILED: inventory full");
                                            transferSuccess = false;
                                            break;
                                        }
                                    }
                                }

                                if (transferSuccess)
                                {
                                    log("    Sequencing: " + spawnTarget.GetId() + " (" + spawnTarget.GetChanceToSpawn() + " %)");

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
                                    log("    Sequencing not possible: could not transfer all ingredients into the inventory");
                                }
                            } 
                            else
                            {
                                log("    Sequencing: Ingredients still missing");
                            }
                        } 
                        else
                        {
                            log("    Sequencing: No applicable DNA sequence found");
                        }
                    }
                    else
                    {
                        log("    Sequencing progress: " + (incubator.GetGrowth()) + " %");
                    }
                }
            }
            else
            {
                log("  No incubators found.");
            }
            log("Done<Incubators>");
        }

        static string GetCategoryFor(string ingredientGroupId)
        {
            if (ingredientGroupId.StartsWith("LarvaeBase"))
            {
                return "Larvae";
            }
            else if (ingredientGroupId.StartsWith("Mutagen"))
            {
                return "Mutagen";
            }
            else if (ingredientGroupId.StartsWith("Fertilizer"))
            {
                return "Fertilizer";
            }
            return "";
        }

        static bool CheckCanCraftWith(string gid, DataConfig.CraftableIn craftableIn, HashSet<string> ingredientSet)
        {
            var gr = GroupsHandler.GetGroupViaId(gid);
            if (gr.GetUnlockingInfos().GetIsUnlocked())
            {
                foreach (var gi in GroupsHandler.GetGroupsItem())
                {
                    if (gi.CanBeCraftedIn(craftableIn) && gi.GetUnlockingInfos().GetIsUnlocked())
                    {
                        var recipe = gi.GetRecipe().GetIngredientsGroupInRecipe();
                        foreach (var rgi in recipe)
                        {
                            if (rgi.id == gid)
                            {
                                foreach (var rgi2 in recipe)
                                {
                                    ingredientSet.Add(rgi2.id);
                                }
                                break;
                            }
                        }
                    }
                }
            }
            return ingredientSet.Count != 0;
        }

        void HandleSequencers()
        {
            log("Begin<Sequencers>");
            // Category keys for various source and target container names
            Dictionary<string, string> keywordMapping = new()
            {
                { "Mutagen", sequencerMutagenId.Value },
                { "TreeRoot", sequencerTreeRootId.Value },
                { "FlowerSeed", sequencerFlowerSeedId.Value },
                { "TreeSeed", sequencerTreeSeedId.Value },
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
                if (gid == "GeneticManipulator1")
                {
                    GetOrCreate(itemCategories, "Sequencer").Add(wo);
                    log("    Sequencer <- " + DebugWorldObject(wo));
                }
            }

            if (itemCategories.TryGetValue("Sequencer", out var sequencerList))
            {
                foreach (var sequencer in sequencerList)
                {
                    log("  Sequencer: " + DebugWorldObject(sequencer));
                    // Try to deposit finished products first
                    Inventory sequencerInv = InventoriesHandler.GetInventoryById(sequencer.GetLinkedInventoryId());

                    var currentItems = sequencerInv.GetInsideWorldObjects();
                    if (currentItems.Count > 0 && sequencer.GetGrowth() == 0)
                    {
                        log("    Depositing products");
                        List<WorldObject> items = new(currentItems);
                        for (int i = items.Count - 1; i >= 0; i--)
                        {
                            WorldObject item = items[i];
                            var gid = item.GetGroup().GetId();
                            if (gid.StartsWith("Tree") && gid.EndsWith("Seed"))
                            {
                                TryDeposit(sequencerInv, item, itemCategories, "TreeSeed");
                            }
                        }
                    }

                    if (sequencer.GetGrowth() == 0)
                    {
                        log("    Collecting ingredients");
                        bool hasFlower = false;
                        bool hasMutagen = false;
                        bool hasRoot = false;

                        foreach (var wo in currentItems)
                        {
                            var gid = wo.GetGroup().GetId();
                            if (gid.StartsWith("Seed"))
                            {
                                hasFlower = true;
                            }
                            hasMutagen |= gid == "Mutagen1";
                            hasRoot |= gid == "TreeRoot";
                        }

                        if (!hasFlower)
                        {
                            hasFlower = TryCollect(sequencerInv, "Seed", itemCategories, "FlowerSeed");
                        }
                        if (!hasMutagen)
                        {
                            hasMutagen = TryCollect(sequencerInv, "Mutagen1", itemCategories, "Mutagen");
                        }
                        if (!hasRoot)
                        {
                            hasRoot = TryCollect(sequencerInv, "TreeRoot", itemCategories, "TreeRoot");
                        }

                        if (hasFlower && hasMutagen && hasRoot)
                        {
                            var spawnTarget = Analyze(currentItems, DataConfig.CraftableIn.CraftGeneticT1);
                            if (spawnTarget != null)
                            {
                                log("    Sequencing: " + spawnTarget.GetId() + " (" + spawnTarget.GetChanceToSpawn() + " %)");

                                sequencer.SetGrowth(1f);
                                sequencer.SetLinkedGroups(new List<Group> { spawnTarget });

                                var t = Time.time + 0.01f;
                                foreach (WorldObject wo in sequencerInv.GetInsideWorldObjects())
                                {
                                    wo.SetLockInInventoryTime(t);
                                }
                                StartCoroutine(RefreshDisplayer(0.1f, sequencerInv));
                            }
                            else
                            {
                                log("    Sequencing: No applicable DNA sequence found");
                            }
                        }
                        else
                        {
                            if (!hasFlower)
                            {
                                log("      Missing Flower Seed ingredient");
                            }
                            if (!hasMutagen)
                            {
                                log("      Missing Mutagen ingredient");
                            }
                            if (!hasRoot)
                            {
                                log("      Missing Tree Root ingredient");
                            }
                        }
                    }
                    else
                    {
                        log("    Sequencing progress: " + (sequencer.GetGrowth()) + " %");
                    }
                }
            }
            else
            {
                log("  No sequencers found.");
            }
            log("Done<Sequencers>");
        }

        GroupItem PickRecipe(DataConfig.CraftableIn craftableIn)
        {
            List<GroupItem> candidates = new();
            foreach (var gi in GroupsHandler.GetGroupsItem())
            {
                if (gi.CanBeCraftedIn(craftableIn) && gi.GetUnlockingInfos().GetIsUnlocked())
                {
                    candidates.Add(gi);
                }
            }

            return PickRandomCandidate(candidates);
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

            return PickRandomCandidate(candidates);
        }

        GroupItem PickRandomCandidate(List<GroupItem> candidates)
        {
            log("    Candidate pool:");
            foreach (var gi in candidates)
            {
                log("      " + gi.id + " (\"" + Readable.GetGroupName(gi) + "\") @ Chance = " + gi.GetChanceToSpawn() + " %");
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

        class IngredientSource
        {
            internal string ingredient;
            internal Inventory source;
            internal WorldObject wo;
        }
        
        void FindIngredientsIn(string gid, Dictionary<string, List<WorldObject>> itemCategories, string itemKey, List<IngredientSource> result)
        {
            if (itemCategories.TryGetValue(itemKey, out var containers))
            {
                foreach (var container in containers)
                {
                    Inventory inv = InventoriesHandler.GetInventoryById(container.GetLinkedInventoryId());

                    foreach (var wo in inv.GetInsideWorldObjects())
                    {
                        var woGid = wo.GetGroup().GetId();
                        if (woGid == gid)
                        {
                            result.Add(new IngredientSource
                            {
                                ingredient = woGid,
                                source = inv,
                                wo = wo
                            });
                        }
                    }
                }
            }
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
                log("      Collect item: <" + gid + "> Failed - no items found");
                return false;
            }
            log("      Collect item: <" + gid + "> Failed - no containers found");
            return false;
        }

        
    }
}
