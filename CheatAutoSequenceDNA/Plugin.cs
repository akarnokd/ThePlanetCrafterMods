// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LibCommon;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static SpaceCraft.AchievementsData;
using static SpaceCraft.DataConfig;

namespace CheatAutoSequenceDNA
{
    [BepInPlugin(modCheatAutoSequenceDNAGuid, "(Cheat) Auto Sequence DNA", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modCheatAutoSequenceDNAGuid = "akarnokd.theplanetcraftermods.cheatautosequencedna";

        static ConfigEntry<bool> incubatorEnabled;

        static ConfigEntry<string> incubatorFertilizerId;

        static ConfigEntry<string> incubatorMutagenId;

        static ConfigEntry<string> incubatorLarvaeId;

        static ConfigEntry<string> incubatorButterflyId;

        static ConfigEntry<string> incubatorBeeId;

        static ConfigEntry<string> incubatorSilkId;

        static ConfigEntry<string> incubatorBacteriaId;

        static ConfigEntry<bool> sequencerEnabled;

        static ConfigEntry<string> sequencerMutagenId;

        static ConfigEntry<string> sequencerTreeRootId;

        static ConfigEntry<string> sequencerFlowerSeedId;

        static ConfigEntry<string> sequencerTreeSeedId;

        static ConfigEntry<string> sequencerVegetableId;

        static ConfigEntry<string> sequencerPhytoplanktonId;

        static ConfigEntry<string> sequencerFertilizerId;

        static ConfigEntry<string> sequencerPurificationId;

        static ConfigEntry<string> incubatorPhytoplanktonId;

        static ConfigEntry<string> incubatorFishId;

        static ConfigEntry<string> incubatorFrogEggId;

        static ConfigEntry<string> incubatorFlowerSeedId;

        static ConfigEntry<string> incubatorMushroomId;

        static ConfigEntry<int> range;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<bool> extractorEnabled;

        static ConfigEntry<string> extractorInput;

        static ConfigEntry<string> extractorOutput;

        static ConfigEntry<int> extractorCount;

        static ConfigEntry<bool> incubatorUnhide;
        static ConfigEntry<bool> sequencerUnhide;

        static ManualLogSource logger;

        static Plugin me;

        static ConfigEntry<bool> debugFixPlanetSwitch;

        static AccessTools.FieldRef<MachineGrowerIfLinkedGroup, LinkedGroupsProxy> fMachineGrowerIfLinkedGroupLinkedGroupsProxy;
        static AccessTools.FieldRef<MachineGrowerIfLinkedGroup, GrowthProxy> fMachineGrowerIfLinkedGroupGrowthProxy;
        static AccessTools.FieldRef<MachineConvertRecipe, LinkedGroupsProxy> fMachineConvertRecipeLinkedGroupsProxy;
        static AccessTools.FieldRef<MachineConvertRecipe, GrowthProxy> fMachineConvertRecipeGrowthProxy;

        static AccessTools.FieldRef<EventHoverShowGroup, Group> fEventHoverShowGroupAssociatedGroup;
        static Font font;

        static readonly Dictionary<int, HashSet<string>> disabledDNA = [];
        static readonly int shadowContainerId = 700300000;

        const string funcSetEnabled = "AutoSequenceDNASetEnabled";
        const string funcSetAll = "AutoSequenceDNASetAll";
        static AccessTools.FieldRef<UiWindowCraft, ActionCrafter> fUiWindowCraftActionCrafter;
        static AccessTools.FieldRef<Inventory, List<WorldObject>> fInventoryWorldObjectsInInventory;

        public void Awake()
        {
            me = this;
            logger = Logger;

            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            sequencerEnabled = Config.Bind("Sequencer", "Enabled", true, "Should the Tree-sequencer auto sequence?");
            
            sequencerMutagenId = Config.Bind("Sequencer", "Mutagen", "*Mutagen", "The name of the container(s) where to look for mutagen.");
            sequencerTreeRootId = Config.Bind("Sequencer", "TreeRoot", "*TreeRoot", "The name of the container(s) where to look for Tree Root.");
            sequencerFlowerSeedId = Config.Bind("Sequencer", "FlowerSeed", "*FlowerSeed", "The name of the container(s) where to look for Flower Seeds (all kinds).");
            sequencerTreeSeedId = Config.Bind("Sequencer", "TreeSeed", "*TreeSeed", "The name of the container(s) where to deposit the spawned tree seeds.");
            sequencerPhytoplanktonId = Config.Bind("Sequencer", "Phytoplankton", "*Phytoplankton", "The name of the container(s) where to look for Phytoplankton.");
            sequencerFertilizerId = Config.Bind("Sequencer", "Fertilizer", "*Fertilizer", "The name of the container(s) where to look for fertilizer.");
            sequencerVegetableId = Config.Bind("Sequencer", "Vegetable", "*Vegetable", "The name of the container(s) where to look for vegetables.");
            sequencerPurificationId = Config.Bind("Sequencer", "Purification", "*Purification", "The name of the container(s) where to look for purification gel.");
            sequencerUnhide = Config.Bind("Sequencer", "Unhide", true, "Unhide the alternative recipes and outputs.");

            incubatorEnabled = Config.Bind("Incubator", "Enabled", true, "Should the Incubator auto sequence?");
            incubatorFertilizerId = Config.Bind("Incubator", "Fertilizer", "*Fertilizer", "The name of the container(s) where to look for fertilizer.");
            incubatorMutagenId = Config.Bind("Incubator", "Mutagen", "*Mutagen", "The name of the container(s) where to look for mutagen.");
            incubatorLarvaeId = Config.Bind("Incubator", "Larvae", "*Larvae", "The name of the container(s) where to look for larvae (common, uncommon, rare).");
            incubatorButterflyId = Config.Bind("Incubator", "Butterfly", "*Butterfly", "The name of the container(s) where to deposit the spawned butterflies.");
            incubatorBeeId = Config.Bind("Incubator", "Bee", "*Bee", "The name of the container(s) where to deposit the spawned bees.");
            incubatorSilkId = Config.Bind("Incubator", "Silk", "*Silk", "The name of the container(s) where to deposit the spawned silk worms.");
            incubatorPhytoplanktonId = Config.Bind("Incubator", "Phytoplankton", "*Phytoplankton", "The name of the container(s) where to look for Phytoplankton.");
            incubatorFishId = Config.Bind("Incubator", "Fish", "*Fish", "The name of the container(s) where to deposit the spawned fish.");
            incubatorFrogEggId = Config.Bind("Incubator", "FrogEgg", "*FrogEgg", "The name of the container(s) where to to look for frog eggs.");
            incubatorBacteriaId = Config.Bind("Incubator", "Bacteria", "*Bacteria", "The name of the container(s) where to to look for bacteria samples.");
            incubatorFlowerSeedId = Config.Bind("Incubator", "FlowerSeed", "*FlowerSeed", "The name of the container(s) where to to look for flower seeds.");
            incubatorMushroomId = Config.Bind("Incubator", "Mushroom", "*Mushroom", "The name of the container(s) where to to look for mushroom.");
            incubatorUnhide = Config.Bind("Incubator", "Unhide", true, "Unhide the alternative recipes and outputs.");

            extractorEnabled = Config.Bind("Extractor", "Enabled", true, "Should the automatic DNA extraction happen?");
            extractorInput = Config.Bind("Extractor", "Input", "*ExtractFrom", "The name of the container to look for items to extract DNA from");
            extractorOutput = Config.Bind("Extractor", "Output", "*ExtractInto", "The name of the container to put the extracted DNA Sequences into");
            extractorCount = Config.Bind("Extractor", "Count", 5, "How many items to process per cycle.");

            debugMode = Config.Bind("General", "DebugMode", false, "Enable debugging with detailed logs (chatty!).");
            range = Config.Bind("General", "Range", 30, "The maximum distance to look for the named containers. 0 means unlimited.");

            debugFixPlanetSwitch = Config.Bind("General", "FixPlanetSwitch", true, "Fix for the vanilla bug with switching planets stopping sequencers/incubators");

            fMachineGrowerIfLinkedGroupGrowthProxy = AccessTools.FieldRefAccess<MachineGrowerIfLinkedGroup, GrowthProxy>("_growthProxy");
            fMachineGrowerIfLinkedGroupLinkedGroupsProxy = AccessTools.FieldRefAccess<MachineGrowerIfLinkedGroup, LinkedGroupsProxy>("_lgProxy");

            fMachineConvertRecipeGrowthProxy = AccessTools.FieldRefAccess<MachineConvertRecipe, GrowthProxy>("_growthProxy");
            fMachineConvertRecipeLinkedGroupsProxy = AccessTools.FieldRefAccess<MachineConvertRecipe, LinkedGroupsProxy>("_lgProxy");

            fEventHoverShowGroupAssociatedGroup = AccessTools.FieldRefAccess<EventHoverShowGroup, Group>("_associatedGroup");

            fUiWindowCraftActionCrafter = AccessTools.FieldRefAccess<UiWindowCraft, ActionCrafter>("sourceCrafter");

            fInventoryWorldObjectsInInventory = AccessTools.FieldRefAccess<Inventory, List<WorldObject>>("_worldObjectsInInventory");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var harmony = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.ModPlanetLoaded.Patch(harmony, modCheatAutoSequenceDNAGuid, _ => PlanetLoader_HandleDataAfterLoad());

            ModNetworking.Init(modCheatAutoSequenceDNAGuid, logger);
            ModNetworking.Patch(harmony);
            ModNetworking._debugMode = debugMode.Value;
            ModNetworking.RegisterFunction(funcSetEnabled, OnSetEnabled);
            ModNetworking.RegisterFunction(funcSetAll, OnSetAll);

            StartCoroutine(SequencerCheckLoop(2.5f));
        }

        static void Log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }
        static void PlanetLoader_HandleDataAfterLoad()
        {
            LibCommon.SaveModInfo.Save();
            RestoreDNASettings();
            if (!(NetworkManager.Singleton?.IsServer ?? true))
            {
                ModNetworking.SendHost(funcSetAll, "");
            }
        }

        IEnumerator SequencerCheckLoop(float delay)
        {
            for (; ; )
            {
                if (NetworkManager.Singleton?.IsServer ?? false)
                {
                    PlayersManager playersManager = Managers.GetManager<PlayersManager>();
                    if (playersManager != null)
                    {
                        PlayerMainController player = playersManager.GetActivePlayerController();
                        if (player != null)
                        {
                            if (NetworkManager.Singleton?.IsServer ?? true)
                            {
                                if (incubatorEnabled.Value)
                                {
                                    yield return HandleIncubators();
                                }
                                if (sequencerEnabled.Value)
                                {
                                    yield return HandleSequencers();
                                }
                                if (extractorEnabled.Value)
                                {
                                    HandleExtractors();
                                }
                            }
                        }
                    }
                }
                
                yield return new WaitForSeconds(delay);
            }
        }

        static string DebugWorldObject(WorldObject wo)
        {
            var str = wo.GetId() + ", " + wo.GetGroup().GetId();
            var txt = wo.GetText();
            if (!string.IsNullOrEmpty(txt))
            {
                str += ", \"" + txt + "\"";
            }
            str += ", " + (wo.GetIsPlaced() ? wo.GetPosition() : "");
            str += ", planet " + wo.GetPlanetHash();
            return str;
        }

        static List<WorldObject> GetOrCreate(Dictionary<string, List<WorldObject>> list, string key)
        {
            if (list.TryGetValue(key, out var result))
            {
                return result;
            }
            result = [];
            list[key] = result;
            return result;
        }

        static void ContainerDiscovery(
            Dictionary<string, string> keywordMapping,
            Dictionary<string, List<WorldObject>> itemCategories,
            string machineId, string machineCategoryId)
        {
            foreach (WorldObject wo in WorldObjectsHandler.Instance.GetConstructedWorldObjects())
            {
                var gid = wo.GetGroup().GetId();
                var txt = wo.GetText() ?? "";
                if (gid == "Container1" || gid == "Container2" || gid == "Container3")
                {
                    foreach (var kv in keywordMapping)
                    {
                        if (txt.Contains(kv.Value))
                        {
                            GetOrCreate(itemCategories, kv.Key).Add(wo);
                            Log("    " + kv.Key + " <- " + DebugWorldObject(wo));
                        }
                    }
                }
                if (gid == machineId)
                {
                    GetOrCreate(itemCategories, machineCategoryId).Add(wo);
                    Log("    "+ machineCategoryId + " <- " + DebugWorldObject(wo));
                }
            }
        }

        IEnumerator HandleIncubators()
        {
            Log("Begin<Incubators>");
            // Category keys for various source and target container names
            Dictionary<string, string> keywordMapping = new()
            {
                { "Fertilizer", incubatorFertilizerId.Value },
                { "Mutagen", incubatorMutagenId.Value },
                { "Larvae", incubatorLarvaeId.Value },
                { "Butterfly", incubatorButterflyId.Value },
                { "Bee", incubatorBeeId.Value },
                { "Silk", incubatorSilkId.Value },
                { "Phytoplankton", incubatorPhytoplanktonId.Value },
                { "Fish", incubatorFishId.Value },
                { "FrogEgg", incubatorFrogEggId.Value },
                { "Bacteria", incubatorBacteriaId.Value },
                { "FlowerSeed", incubatorFlowerSeedId.Value },
                { "Mushroom", incubatorMushroomId.Value },

            };

            // List of world objects per category (containers, machines)
            Dictionary<string, List<WorldObject>> itemCategories = [];

            Log("  Container discovery");
            ContainerDiscovery(keywordMapping, itemCategories, "Incubator1", "Incubator");

            if (itemCategories.TryGetValue("Incubator", out var incubatorList))
            {
                foreach (var incubator in incubatorList)
                {
                    Log("  Incubator: " + DebugWorldObject(incubator));
                    var machineId = incubator.GetId();
                    // Try to deposit finished products first
                    Inventory incubatorInv = InventoriesHandler.Instance.GetInventoryById(incubator.GetLinkedInventoryId());
                    if (incubatorInv == null)
                    {
                        continue;
                    }
                    int incubatorPlanetHash = incubator.GetPlanetHash();

                    var currentItems = fInventoryWorldObjectsInInventory(incubatorInv);
                    if (currentItems.Count > 0 && incubator.GetGrowth() == 0)
                    {
                        Log("    Depositing products");
                        List<WorldObject> items = [.. currentItems];
                        for (int i = items.Count - 1; i >= 0; i--)
                        {
                            WorldObject item = items[i];
                            var gid = item.GetGroup().GetId();
                            if (gid.StartsWith("Butterfly", StringComparison.Ordinal))
                            {
                                TryDeposit(incubatorInv, item, itemCategories, "Butterfly", incubatorPlanetHash);
                            }
                            if (gid.StartsWith("Bee", StringComparison.Ordinal))
                            {
                                TryDeposit(incubatorInv, item, itemCategories, "Bee", incubatorPlanetHash);
                            }
                            if (gid.StartsWith("Silk", StringComparison.Ordinal))
                            {
                                TryDeposit(incubatorInv, item, itemCategories, "Silk", incubatorPlanetHash);
                            }
                            if (gid.StartsWith("Fish", StringComparison.Ordinal))
                            {
                                TryDeposit(incubatorInv, item, itemCategories, "Fish", incubatorPlanetHash);
                            }
                            if (gid.StartsWith("Frog", StringComparison.Ordinal) && gid.EndsWith("Eggs", StringComparison.Ordinal))
                            {
                                TryDeposit(incubatorInv, item, itemCategories, "FrogEgg", incubatorPlanetHash);
                            }
                            if (gid.StartsWith("LarvaeBase", StringComparison.Ordinal))
                            {
                                TryDeposit(incubatorInv, item, itemCategories, "Larvae", incubatorPlanetHash);
                            }
                        }
                    }

                    if (incubator.GetGrowth() == 0)
                    {
                        Log("    Picking Recipe");

                        var candidates = GetCandidates(DataConfig.CraftableIn.CraftIncubatorT1, machineId);
                        Shuffle(candidates);
                        var found = false;
                        foreach (var spawnTarget in candidates)
                        {
                            if (StartNewResearch(spawnTarget, currentItems,
                                itemCategories, incubatorInv, incubator))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (candidates.Count == 0)
                        {
                            Log("    Sequencing: No applicable DNA sequence found");
                        }
                        if (!found)
                        {
                            Log("    Sequencing: No complete set of ingredients for any DNA sequence found");
                        }

                    }
                    else
                    {
                        var growth = incubator.GetGrowth();
                        Log("    Sequencing progress: " + growth + " % for " + string.Join(", ", (incubator.GetLinkedGroups() ?? []).Select(g => g.id)));
                        if (growth < 100)
                        {
                            InventoriesHandler.Instance.LockInventoryContent(incubatorInv, true, 0f, null);
                        }
                    }
                    yield return null;
                }
            }
            else
            {
                Log("  No incubators found.");
            }
            Log("Done<Incubators>");
        }

        bool StartNewResearch(
            GroupItem spawnTarget,
            IEnumerable<WorldObject> currentItems, 
            Dictionary<string, List<WorldObject>> itemCategories, 
            Inventory machineInventory,
            WorldObject machine)
        {
            Log("    Picked: " + spawnTarget.id + " (\"" + Readable.GetGroupName(spawnTarget) + "\") @ Chance = " + spawnTarget.GetChanceToSpawn() + " %");

            List<Group> ingredients = [.. spawnTarget.GetRecipe().GetIngredientsGroupInRecipe()];
            List<WorldObject> available = [.. currentItems];
            List<Group> missing = [];

            int ingredientsFulfilled = 0;

            Log("      Checking inventory for ingredients");
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
                    Log("        Not in inventory: " + ingredient.id);
                    missing.Add(ingredient);
                }
                else
                {
                    ingredientsFulfilled++;
                    Log("        Found in inventory: " + ingredient.id);
                }
            }

            List<IngredientSource> toTransfer = [];

            if (missing.Count != 0)
            {
                Log("      Checking containers for missing ingredients");
                Dictionary<string, List<IngredientSource>> sourcesPerCategory = [];

                foreach (var m in missing)
                {
                    var cat = GetCategoryFor(m.id);
                    Log("        Checking sources for ingredient category: " + cat + " for " + m.id);
                    if (!sourcesPerCategory.TryGetValue(cat, out var ingredientSources))
                    {
                        Log("          Searching");
                        ingredientSources = [];
                        sourcesPerCategory[cat] = ingredientSources;
                    }
                    FindIngredientsIn(m.id, itemCategories, cat, ingredientSources, machine.GetPosition(), machine.GetPlanetHash(), machineInventory.GetSize()); ;
                }

                foreach (var m in missing)
                {
                    var cat = GetCategoryFor(m.id);
                    var sources = sourcesPerCategory[cat];
                    Log("        Looking for ingredient in sources: " + m.id + " (" + cat + ")");
                    //log("        " + string.Join(",", sources.Where(g => g != null).Select(g => g.ingredient)));
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
                            Log("        Ingredient Found " + m.id);
                            break;
                        }
                    }
                    if (!found)
                    {
                        Log("        No source for ingredient " + m.id);
                    }
                }
            }

            Log("      Recipe check: Found = " + ingredientsFulfilled + ", Required = " + ingredients.Count);
            if (ingredientsFulfilled == ingredients.Count)
            {
                bool transferSuccess = true;
                if (toTransfer.Count != 0)
                {
                    Log("        Transferring ingredients");
                    foreach (var tt in toTransfer)
                    {
                        if (machineInventory.AddItem(tt.wo))
                        {
                            Log("          From " + tt.source.GetId() + ": " + DebugWorldObject(tt.wo) + " SUCCESS");
                            tt.source.RemoveItem(tt.wo);
                        }
                        else
                        {
                            Log("          From " + tt.source.GetId() + ": " + DebugWorldObject(tt.wo) + " FAILED: inventory full");
                            transferSuccess = false;
                            break;
                        }
                    }
                }

                if (transferSuccess)
                {
                    Log("      Sequencing: " + spawnTarget.GetId() + " (" + spawnTarget.GetChanceToSpawn() + " %)");

                    machine.GetGameObject().GetComponentInChildren<GrowthProxy>().SetGrowth(1f);
                    machine.GetGameObject().GetComponentInChildren<LinkedGroupsProxy>().SetLinkedGroups([spawnTarget]);

                    var t = Time.time + 0.01f;
                    InventoriesHandler.Instance.LockInventoryContent(machineInventory, true, 0f, null);

                    return true;
                }
                else
                {
                    Log("      Sequencing not possible: could not transfer all ingredients into the inventory");
                }
            }
            else
            {
                Log("      Sequencing: Ingredients still missing");
            }
            return false;
        }

        static string GetCategoryFor(string ingredientGroupId)
        {
            if (ingredientGroupId.StartsWith("LarvaeBase", StringComparison.Ordinal))
            {
                return "Larvae";
            }
            else if (ingredientGroupId.StartsWith("Mutagen", StringComparison.Ordinal))
            {
                return "Mutagen";
            }
            else if (ingredientGroupId.StartsWith("Fertilizer", StringComparison.Ordinal))
            {
                return "Fertilizer";
            }
            else if (ingredientGroupId.StartsWith("Seed", StringComparison.Ordinal))
            {
                return "FlowerSeed";
            }
            else if (ingredientGroupId.StartsWith("TreeRoot", StringComparison.Ordinal))
            {
                return "TreeRoot";
            }
            else if (ingredientGroupId.StartsWith("Phytoplankton", StringComparison.Ordinal))
            {
                return "Phytoplankton";
            }
            else if (ingredientGroupId.StartsWith("Frog", StringComparison.Ordinal) && ingredientGroupId.EndsWith("Eggs", StringComparison.Ordinal))
            {
                return "FrogEgg";
            }
            else if (ingredientGroupId.StartsWith("Bacteria1", StringComparison.Ordinal))
            {
                return "Bacteria";
            }
            else if (ingredientGroupId.StartsWith("Vegetable", StringComparison.Ordinal) && ingredientGroupId.EndsWith("Growable", StringComparison.Ordinal))
            {
                return "Vegetable";
            }
            else if (ingredientGroupId.StartsWith("PurificationGel", StringComparison.Ordinal))
            {
                return "Purification";
            }
            else if (ingredientGroupId.Contains("Mushroom", StringComparison.Ordinal))
            {
                return "Mushroom";
            }
            return "";
        }

        IEnumerator HandleSequencers()
        {
            Log("Begin<Sequencers>");
            // Category keys for various source and target container names
            Dictionary<string, string> keywordMapping = new()
            {
                { "Mutagen", sequencerMutagenId.Value },
                { "TreeRoot", sequencerTreeRootId.Value },
                { "FlowerSeed", sequencerFlowerSeedId.Value },
                { "TreeSeed", sequencerTreeSeedId.Value },
                { "Phytoplankton", sequencerPhytoplanktonId.Value },
                { "Fertilizer", sequencerFertilizerId.Value },
                { "Vegetable", sequencerVegetableId.Value },
                { "Purification", sequencerPurificationId.Value }
            };

            // List of world objects per category (containers, machines)
            Dictionary<string, List<WorldObject>> itemCategories = [];

            Log("  Container discovery");
            ContainerDiscovery(keywordMapping, itemCategories, "GeneticManipulator1", "Sequencer");

            if (itemCategories.TryGetValue("Sequencer", out var sequencerList))
            {
                foreach (var sequencer in sequencerList)
                {
                    Log("  Sequencer: " + DebugWorldObject(sequencer));
                    var machineId = sequencer.GetId();
                    // Try to deposit finished products first
                    Inventory sequencerInv = InventoriesHandler.Instance.GetInventoryById(sequencer.GetLinkedInventoryId());
                    if (sequencerInv == null)
                    {
                        continue;
                    }
                    int sequencerPlanetHash = sequencer.GetPlanetHash();

                    var currentItems = fInventoryWorldObjectsInInventory(sequencerInv);
                    if (currentItems.Count > 0 && sequencer.GetGrowth() == 0)
                    {
                        Log("    Depositing products");
                        List<WorldObject> items = [.. currentItems];
                        for (int i = items.Count - 1; i >= 0; i--)
                        {
                            WorldObject item = items[i];
                            var gid = item.GetGroup().GetId();
                            if (gid.StartsWith("Tree", StringComparison.Ordinal) && gid.EndsWith("Seed", StringComparison.Ordinal))
                            {
                                TryDeposit(sequencerInv, item, itemCategories, "TreeSeed", sequencerPlanetHash);
                            }
                            if (gid.StartsWith("Seed", StringComparison.Ordinal))
                            {
                                TryDeposit(sequencerInv, item, itemCategories, "FlowerSeed", sequencerPlanetHash);
                            }
                        }
                    }

                    if (sequencer.GetGrowth() == 0)
                    {
                        var candidates = GetCandidates(CraftableIn.CraftGeneticT1, machineId);

                        if (candidates.Count != 0)
                        {
                            Shuffle(candidates);

                            Log("    Sequencing: candidates: " + string.Join(", ", candidates.Select(g => g.id)));

                            bool found = false;
                            foreach (var spawnTarget in candidates)
                            {
                                if (StartNewResearch(spawnTarget, currentItems, itemCategories, sequencerInv, sequencer))
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                            {
                                Log("    Sequencing: No complete set of ingredients for any DNA sequence found");
                            }
                        }
                        else
                        {
                            Log("    Sequencing: No applicable DNA sequence found");
                        }
                    }
                    else
                    {
                        var growth = sequencer.GetGrowth();
                        Log("    Sequencing progress: " + growth + " % for " + string.Join(", ", (sequencer.GetLinkedGroups() ?? []).Select(g => g.id)));
                        if (growth < 100)
                        {
                            InventoriesHandler.Instance.LockInventoryContent(sequencerInv, true, 0f, null);
                        }

                    }
                    yield return null;
                }
            }
            else
            {
                Log("  No sequencers found.");
            }
            Log("Done<Sequencers>");
        }

        List<GroupItem> GetCandidates(DataConfig.CraftableIn craftableIn, int machineId)
        {
            List<GroupItem> candidates = [];
            foreach (var gi in GroupsHandler.GetGroupsItem())
            {
                if (gi.CanBeCraftedIn(craftableIn) 
                    && (gi.GetUnlockingInfos().GetIsUnlocked() || gi.GetIsGloballyUnlocked())
                    && IsDNAEnabled(machineId, gi.id))
                {
                    candidates.Add(gi);
                }
            }
            return candidates;
        }

        void TryDeposit(
            Inventory source, 
            WorldObject item, 
            Dictionary<string, List<WorldObject>> itemCategories, 
            string itemKey,
            int sourcePlanetHash)
        {
            Log("      Deposit item: " + DebugWorldObject(item));
            if (itemCategories.TryGetValue(itemKey, out var containers))
            {
                if (containers.Count != 0)
                {
                    new DeferredDepositor(item, source, sourcePlanetHash, containers.GetEnumerator())
                        .Drain();
                    return;
                }
            }
            Log("        Into      : Failed - no target containers found");
        }

        void HandleExtractors()
        {
            List<WorldObject> extractors = [];
            List<WorldObject> inputs = [];
            List<WorldObject> outputs = [];
            var inputName = extractorInput.Value;
            var outputName = extractorOutput.Value;
            var maxDistance = range.Value;
            var woh = WorldObjectsHandler.Instance;
            var gth = GeneticTraitHandler.Instance;
            var invh = InventoriesHandler.Instance;
            var remaining = extractorCount.Value;

            Log("Begin<Extractors>");
            foreach (var wo in woh.GetConstructedWorldObjects())
            {
                var gr = wo.GetGroup().id;

                if (gr == "GeneticExtractor1")
                {
                    extractors.Add(wo);
                }
                else
                if (gr == "Container1" || gr == "Container2" || gr == "Container3")
                {
                    var nm = wo.GetText() ?? "";
                    if (nm.Contains(inputName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        inputs.Add(wo);
                    }
                    if (nm.Contains(outputName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        outputs.Add(wo);
                    }
                }
            }
            Log("    Extractors Found: " + extractors.Count);
            Log("    Inputs Found    : " + inputs.Count);
            Log("    Outputs Found   : " + outputs.Count);

            foreach (var extractor in extractors)
            {
                Log("  Extractor: " + DebugWorldObject(extractor));

                var p = extractor.GetPosition();
                var hash = extractor.GetPlanetHash();

                var anyInput = false;

                foreach (var input in inputs)
                {
                    var d = Vector3.Distance(input.GetPosition(), p);
                    if (hash == input.GetPlanetHash() && (maxDistance <= 0 || d <= maxDistance))
                    {
                        anyInput = true;
                        var inv = invh.GetInventoryById(input.GetLinkedInventoryId());
                        var content = fInventoryWorldObjectsInInventory(inv);
                        Log("  - Checking input " + input.GetId() + " with " + inv.GetId() + " at " + d + ", Count: " + content.Count);
                        for (var i = content.Count - 1; i >= 0; i--)
                        {
                            var wo = content[i];
                            var gr = wo.GetGroup();
                            if (gr is GroupItem gi)
                            {
                                var trait = gth.GetGeneticTraitExtractedFromGroup(gi);
                                if (trait != null)
                                {
                                    var traitWo = gth.CreateNewTraitWorldObject(trait, true);
                                    traitWo.SetPositionAndRotation(new Vector3(0.1f, 0.1f, 0.1f), Quaternion.identity);
                                    Log("    Item: " + inv.GetId() + " / " + wo.GetId() + " (" + gr.id + ") -> Trait: " + trait.traitType + ", " + trait.traitColor + ", " + trait.traitValue);

                                    var found = false;
                                    foreach (var outWo in outputs)
                                    {
                                        var outInv = invh.GetInventoryById(outWo.GetLinkedInventoryId());
                                        var d2 = Vector2.Distance(outWo.GetPosition(), p);
                                        Log("    - Checking output " + outWo.GetId()+ " with " + outInv.GetId() + " at " + d2);
                                        if (hash == outWo.GetPlanetHash() && (maxDistance <= 0 || d2 <= maxDistance))
                                        {
                                            var wasSuccess = false;
                                            invh.AddWorldObjectToInventory(traitWo, outInv, false, success =>
                                            {
                                                wasSuccess = success;
                                            });

                                            if (wasSuccess)
                                            {
                                                Log("      Inventory: " + outInv.GetId() + " deposited");
                                                found = true;
                                                remaining--;
                                                invh.RemoveItemFromInventory(wo, inv, true);
                                                break;
                                            }
                                        }
                                    }
                                    if (!found)
                                    {
                                        Log("      Inventory: Not found or all full");
                                        WorldObjectsHandler.Instance.DestroyWorldObject(traitWo);
                                    }
                                    else
                                    {
                                        if (remaining <= 0)
                                        {
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    Log("    Item: " + inv.GetId() + " / " + wo.GetId() + " (" + gr.id + ") -> No trait");
                                }
                            }
                        }
                        if (remaining <= 0)
                        {
                            break;
                        }
                    }
                }
                if (!anyInput)
                {
                    Log("    No input inventories in range.");
                }
            }
            Log("Done<Extractors>");
        }

        class IngredientSource
        {
            internal string ingredient;
            internal Inventory source;
            internal WorldObject wo;
        }
        
        void FindIngredientsIn(string gid, 
            Dictionary<string, List<WorldObject>> itemCategories, 
            string itemKey, 
            List<IngredientSource> result,
            Vector3 incubatorDistance,
            int incubatorPlanetHash,
            int maxResults)
        {
            var maxDistance = range.Value;
            if (itemCategories.TryGetValue(itemKey, out var containers))
            {
                int found = 0;
                foreach (var container in containers)
                {
                    if (maxDistance > 0 && Vector3.Distance(container.GetPosition(), incubatorDistance) > maxDistance)
                    {
                        continue;
                    }
                    if (incubatorPlanetHash != container.GetPlanetHash())
                    {
                        continue;
                    }
                    Inventory inv = InventoriesHandler.Instance.GetInventoryById(container.GetLinkedInventoryId());

                    if (inv != null)
                    {
                        foreach (var wo in fInventoryWorldObjectsInInventory(inv))
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
                                found++;
                                if (found >= maxResults)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    if (found >= maxResults)
                    {
                        break;
                    }
                }
            }
        }

        static void Shuffle<T>(IList<T> list)
        {
            for (int n = list.Count - 1; n > 0; n--)
            {
                var k = UnityEngine.Random.Range(0, n + 1);

                (list[n], list[k]) = (list[k], list[n]);
            }
        }

        class DeferredDepositor
        {
            internal WorldObject item;
            internal Inventory source;
            internal int sourcePlanetHash;
            internal IEnumerator<WorldObject> candidatesEnumerator;
            int wip;
            WorldObject current;

            internal DeferredDepositor(WorldObject item, Inventory source, int sourcePlanetHash, IEnumerator<WorldObject> candidatesEnumerator)
            {
                this.item = item;
                this.source = source;
                this.sourcePlanetHash = sourcePlanetHash;
                this.candidatesEnumerator = candidatesEnumerator;
            }

            internal void Drain()
            {
                if (wip++ != 0)
                {
                    return;
                }

                for (; ; )
                {
                    if (current == null)
                    {
                        if (candidatesEnumerator.MoveNext())
                        {
                            current = candidatesEnumerator.Current;
                        }
                        else
                        {
                            Log("        Into      : Failed - all target containers are full");
                            break;
                        }

                        if (current.GetPlanetHash() != sourcePlanetHash)
                        {
                            current = null;
                            continue;
                        }

                        var inv = InventoriesHandler.Instance.GetInventoryById(current.GetLinkedInventoryId());
                        if (inv != null)
                        {
                            InventoriesHandler.Instance.TransferItem(source, inv, item, OnResult);
                        }
                        else
                        {
                            current = null;
                            continue;
                        }
                    }

                    if (--wip == 0)
                    {
                        break;
                    }
                }
            }

            void OnResult(bool success)
            {
                if (success)
                {
                    Log("        Into      : " + DebugWorldObject(current));
                    current = null;
                }
                else
                {
                    current = null;
                    Drain();
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StaticDataHandler), "LoadStaticData")]
        private static void StaticDataHandler_LoadStaticData(List<GroupData> ___groupsData)
        {
            if (!debugFixPlanetSwitch.Value)
            {
                return;
            }
            foreach (var gd in ___groupsData)
            {
                if (gd.associatedGameObject != null 
                    && gd.associatedGameObject.GetComponent<MachineGrowerIfLinkedGroup>() != null
                    && gd.associatedGameObject.GetComponent<MachineConvertRecipe>() != null
                    && gd.associatedGameObject.GetComponent<OffPlanetUpdater>() == null
                    )
                {
                    Log("Adding OffPlanetUpdater to " + gd.id);

                    gd.associatedGameObject.AddComponent<OffPlanetUpdater>();
                }

                if (gd is GroupDataItem gdi && gdi.hideInCrafter 
                    && (
                        (sequencerUnhide.Value && gdi.craftableInList.Contains(CraftableIn.CraftGeneticT1))
                        || (incubatorUnhide.Value && gdi.craftableInList.Contains(CraftableIn.CraftIncubatorT1))
                        )) {
                    Log("Unhiding " + gd.id);
                    gd.hideInCrafter = false;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowCraft), "CreateGrid")]
        static void UiWindowCraft_CreateGrid(GridLayoutGroup ___grid, 
            ActionCrafter ___sourceCrafter)
        {
            if (___sourceCrafter.GetCrafterIdentifier() != CraftableIn.CraftGeneticT1
                && ___sourceCrafter.GetCrafterIdentifier() != CraftableIn.CraftIncubatorT1) {
                return;
            }

            var wo = ___sourceCrafter.GetComponentInParent<WorldObjectAssociated>(true).GetWorldObject();

            foreach (Transform tr in ___grid.transform)
            {
                var parent = tr.gameObject.transform;
                var ehg = tr.gameObject.GetComponent<EventHoverShowGroup>();
                var rt = tr.gameObject.GetComponent<RectTransform>();
                if (ehg != null)
                {
                    if (fEventHoverShowGroupAssociatedGroup(ehg) is GroupItem gi)
                    {
                        var go = new GameObject("DisableAutoDNA");
                        go.transform.SetParent(parent, false);

                        var text = go.AddComponent<Text>();
                        text.font = font;
                        text.color = new Color(1f, 0f, 0f, 1f);
                        text.fontSize = (int)rt.sizeDelta.x;
                        text.text = "X";
                        text.resizeTextForBestFit = false;
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                        text.horizontalOverflow = HorizontalWrapMode.Overflow;
                        text.alignment = TextAnchor.MiddleCenter;

                        go.SetActive(!IsDNAEnabled(wo.GetId(), gi.id));
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowCraft), "OnImageClicked")]
        static bool UiWindowCraft_OnImageClicked(
            EventTriggerCallbackData _eventTriggerCallbackData,
            ActionCrafter ___sourceCrafter)
        {
            if (((___sourceCrafter.GetCrafterIdentifier() == CraftableIn.CraftGeneticT1 && sequencerEnabled.Value)
                || (___sourceCrafter.GetCrafterIdentifier() == CraftableIn.CraftIncubatorT1 && incubatorEnabled.Value)) 
                && _eventTriggerCallbackData.pointerEventData.button == PointerEventData.InputButton.Left)
            {
                var wo = ___sourceCrafter.GetComponentInParent<WorldObjectAssociated>(true).GetWorldObject();
                var gr = _eventTriggerCallbackData.group;

                var enabled = IsDNAEnabled(wo.GetId(), gr.id);
                // Log(gr.id + " currently " + enabled);
                SetDNAEnabled(wo.GetId(), gr.id, !enabled);
                SaveDNASettings();

                var go = _eventTriggerCallbackData.pointerEventData.pointerPress;
                go.transform.Find("DisableAutoDNA").gameObject.SetActive(enabled);

                var msg = wo.GetId() + "," + gr.id + "," + (enabled ? 0 : 1);
                if (NetworkManager.Singleton?.IsServer ?? true)
                {
                    ModNetworking.SendAllClients(funcSetEnabled, msg);
                }
                else
                {
                    ModNetworking.SendHost(funcSetEnabled, msg);
                }

                return false;
            }
            return true;
        }

        static bool IsDNAEnabled(int machineId, string groupId)
        {
            return !(disabledDNA.TryGetValue(machineId, out var set) && set.Contains(groupId));
        }

        static void SetDNAEnabled(int machineId, string groupId, bool state)
        {
            if (state)
            {
                if (disabledDNA.TryGetValue(machineId, out var set))
                {
                    set.Remove(groupId);
                    if (set.Count == 0)
                    {
                        disabledDNA.Remove(machineId);
                    }
                }
            } else
            {
                if (!disabledDNA.TryGetValue(machineId, out var set))
                {
                    set = [];
                    disabledDNA.Add(machineId, set);
                }
                set.Add(groupId);
            }
        }

        static WorldObject EnsureHiddenContainer()
        {
            var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(shadowContainerId);
            if (wo == null)
            {
                wo = WorldObjectsHandler.Instance.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Iron"), shadowContainerId);
                wo.SetText("");
            }
            wo.SetDontSaveMe(false);
            return wo;
        }

        static void SaveDNASettings()
        {
            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                var str = disabledDNA.Join(e => e.Key.ToString() + "=" + string.Join(",", e.Value), ";");
                var wo = EnsureHiddenContainer();
                wo.SetText(str);
            }
        }

        static void RestoreDNASettings()
        {
            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                var wo = EnsureHiddenContainer();
                ParseDNASettings(wo.GetText());
            }
        }

        static void ParseDNASettings(string str)
        {
            disabledDNA.Clear();
            if (str == null || str.Length == 0)
            {
                return;
            }
            foreach (var idSet in str.Split(';'))
            {
                var idAndSet = idSet.Split('=');
                if (idAndSet.Length == 2)
                {
                    var set = idAndSet[1].Split(',');
                    if (int.TryParse(idAndSet[0], out var id))
                    {
                        foreach (var s in set)
                        {
                            SetDNAEnabled(id, s, false);
                        }
                    }
                }
            }
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            ModNetworking._debugMode = debugMode.Value;
        }

        static void OnSetEnabled(ulong sender, string parameters)
        {
            var idAndSet = parameters.Split(',');
            if (idAndSet.Length == 3)
            {
                if (int.TryParse(idAndSet[0], out var id))
                {
                    SetDNAEnabled(id, idAndSet[1], "1" == idAndSet[2]);
                    SaveDNASettings();
                    UpdateCraftWindow(id);

                    if (NetworkManager.Singleton?.IsServer ?? true)
                    {
                        ModNetworking.SendAllClients(funcSetEnabled, parameters);
                    }
                }
            }
        }
        static void OnSetAll(ulong sender, string parameters)
        {
            if (NetworkManager.Singleton?.IsServer ?? true)
            {
                var str = disabledDNA.Join(e => e.Key.ToString() + "=" + string.Join(",", e.Value), ";");
                ModNetworking.SendClient(sender, funcSetAll, str);
            }
            else
            {
                ParseDNASettings(parameters);

                foreach (var mc in disabledDNA.Keys)
                {
                    UpdateCraftWindow(mc);
                }
            }
        }

        static void UpdateCraftWindow(int machineId)
        {
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }
            var win = wh.GetOpenedUi();
            if (win != UiType.Craft)
            {
                return;
            }
            var ui = (UiWindowCraft)wh.GetWindowViaUiId(win);

            var crafter = fUiWindowCraftActionCrafter(ui);

            var machine = crafter.GetComponentInParent<WorldObjectAssociated>(true).GetWorldObject();

            if (machine.GetId() != machineId)
            {
                return;
            }

            foreach (Transform tr in ui.grid.transform)
            {
                var ehg = tr.gameObject.GetComponent<EventHoverShowGroup>();
                var gr = fEventHoverShowGroupAssociatedGroup(ehg);
                var x = tr.Find("DisableAutoDNA");

                if (gr is GroupItem && x != null)
                {
                    x.gameObject.SetActive(!IsDNAEnabled(machineId, gr.id));
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            disabledDNA.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }
        internal class OffPlanetUpdater : MonoBehaviour
        {
            internal void Awake()
            {
                var gp = GetComponentInParent<GrowthProxy>(true);
                var lp = GetComponentInParent<LinkedGroupsProxy>(true);

                var machineGrowerIfLinkedGroup = GetComponent<MachineGrowerIfLinkedGroup>();
                var machineConvertRecipe = GetComponent<MachineConvertRecipe>();

                fMachineGrowerIfLinkedGroupGrowthProxy(machineGrowerIfLinkedGroup) = gp;
                fMachineGrowerIfLinkedGroupLinkedGroupsProxy(machineGrowerIfLinkedGroup) = lp;

                fMachineConvertRecipeGrowthProxy(machineConvertRecipe) = gp;
                fMachineConvertRecipeLinkedGroupsProxy(machineConvertRecipe) = lp;
            }
        }
    }
}
