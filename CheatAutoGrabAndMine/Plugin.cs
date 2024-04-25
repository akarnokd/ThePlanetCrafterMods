// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using UnityEngine.InputSystem;
using UnityEngine;
using System.Collections;
using BepInEx.Logging;
using System.Linq;
using LibCommon;

namespace CheatAutoGrabAndMine
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautograbandmine", "(Cheat) Auto Grab and Mine", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static Plugin me;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<int> range;

        static ConfigEntry<string> includeList;

        static ConfigEntry<string> excludeList;

        static ConfigEntry<string> key;

        static ConfigEntry<int> scanPeriod;

        static ConfigEntry<bool> scanEnabled;

        static ConfigEntry<bool> grabLarvae;

        static ConfigEntry<bool> grabFrogEggs;

        static ConfigEntry<bool> grabFood;

        static ConfigEntry<bool> grabAlgae;

        static ConfigEntry<bool> grabFishEggs;

        static ConfigEntry<bool> mineMinerals;

        static ConfigEntry<bool> grabRods;

        static InputAction toggleAction;

        static ManualLogSource logger;

        static Coroutine scanningCoroutine;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;
            me = this;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging? Chatty!");
            range = Config.Bind("General", "Range", 20, "The range to look for items within.");
            key = Config.Bind("General", "Key", "<Keyboard>/V", "The input action shortcut to toggle automatic scanning and taking.");
            scanPeriod = Config.Bind("General", "Period", 3, "How often scan the surroundings for items go grab or mine. Seconds");
            scanEnabled = Config.Bind("General", "Scanning", false, "If true, the mod is actively scanning for items to take.");

            includeList = Config.Bind("General", "IncludeList", "", "The comma separated list of case-sensitive item ids to include only. If empty, all items are considered except the those listed in ExcludeList.");
            excludeList = Config.Bind("General", "ExcludeList", "", "The comma separated list of case-sensitive item ids to exclude. Only considered if IncludeList is empty.");

            grabLarvae = Config.Bind("General", "Larvae", true, "If true, nearby larvae can be grabbed. Subject to Include/Exclude though.");
            grabFrogEggs = Config.Bind("General", "FrogEggs", true, "If true, nearby frog eggs can be grabbed. Subject to Include/Exclude though.");
            grabFishEggs = Config.Bind("General", "FishEggs", true, "If true, nearby fish eggs can be grabbed. Subject to Include/Exclude though.");
            grabFood = Config.Bind("General", "Food", false, "If true, nearby food can be grabbed. Subject to Include/Exclude though.");
            grabAlgae = Config.Bind("General", "Algae", false, "If true, nearby algae can be grabbed. Subject to Include/Exclude though.");

            mineMinerals = Config.Bind("General", "Minerals", true, "If true, nearby minerals can be mined. Subject to Include/Exclude though.");
            grabRods = Config.Bind("General", "Rods", true, "If true, nearby rods can be grabbed. Subject to Include/Exclude though.");

            if (!key.Value.Contains("<"))
            {
                key.Value = "<Keyboard>/" + key.Value;
            }
            toggleAction = new InputAction(name: "Toggle periodic scan & grab", binding: key.Value);
            toggleAction.Enable();

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));

            OnModConfigChanged(null);
        }

        static void Log(object message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        public void Update()
        {
            var wh = Managers.GetManager<WindowsHandler>();
            if (wh == null)
            {
                return;
            }
            if (wh.GetHasUiOpen())
            {
                return;
            }
            if (toggleAction.WasPressedThisFrame())
            {
                if (Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed)
                {
                    Managers.GetManager<BaseHudHandler>()
                        ?.DisplayCursorText("", 3f, "Auto Grab And Mine: Scanning Now");

                    DoScan();
                }
                else
                {
                    scanEnabled.Value = !scanEnabled.Value;

                    Managers.GetManager<BaseHudHandler>()
                        ?.DisplayCursorText("", 3f, "Auto Grab And Mine: " + (scanEnabled.Value ? "Scan Activated" : "Scan Deactivated"));
                }
            }
        }

        public static void OnModConfigChanged(ConfigEntryBase _)
        {
            if (scanningCoroutine != null)
            {
                me.StopCoroutine(scanningCoroutine);
                scanningCoroutine = null;
            }
            scanningCoroutine = me.StartCoroutine(ScanLoop());
        }

        static IEnumerator ScanLoop()
        {
            var wait = new WaitForSeconds(scanPeriod.Value);
            for (; ; )
            {
                if (scanEnabled.Value)
                {
                    DoScan();
                }
                yield return wait;
            }
        }


        static void DoScan()
        {
            if (!modEnabled.Value)
            {
                return;
            }

            var pm = Managers.GetManager<PlayersManager>();
            if (pm == null)
            {
                return;
            }

            var ac = pm.GetActivePlayerController();
            if (ac == null)
            {
                return;
            }

            Log("Begin");

            var includeGroupIds = includeList.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length != 0).ToHashSet();
            var excludeGroupIds = excludeList.Value.Split(',').Select(v => v.Trim()).Where(v => v.Length != 0).ToHashSet();

            var backpackInv = ac.GetPlayerBackpack().GetInventory();
            var pos = ac.transform.position;

            // ============================================================================================================================

            Log("- Grabables");

            foreach (var grabable in FindObjectsByType<ActionGrabable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (Vector3.Distance(pos, grabable.transform.position) <= range.Value)
                {
                    if (grabable.GetCanGrab())
                    {
                        var woa = grabable.GetComponentInParent<WorldObjectAssociated>();
                        if (woa != null)
                        {
                            var wo = woa.GetWorldObject();
                            if (wo == null)
                            {
                                var woap = grabable.GetComponentInParent<WorldObjectAssociatedProxy>();
                                if (woap != null)
                                {
                                    var grabable2 = grabable;
                                    woap.GetId(id =>
                                    {
                                        var wo2 = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                                        if (wo2 == null)
                                        {
                                            var nc = grabable2.GetComponentInParent<GroupNetworkContainer>();
                                            if (nc != null)
                                            {
                                                wo2 = WorldObjectsHandler.Instance.CreateNewWorldObject(nc.GetGroup(), id, nc.gameObject);
                                            }
                                        }
                                        if (wo2 != null)
                                        {
                                            AddToInventoryFiltered(wo2, grabable2);
                                        }
                                    });
                                }
                            }
                            else
                            {
                                AddToInventoryFiltered(wo, grabable);
                            }
                        }
                    }
                }
            }

            // ============================================================================================================================

            if (mineMinerals.Value)
            {
                Log("- Minerals");
                
                foreach (var minable in FindObjectsByType<ActionMinable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (Vector3.Distance(pos, minable.transform.position) <= range.Value)
                    {
                        var woa = minable.GetComponent<WorldObjectAssociated>();
                        if (woa != null)
                        {
                            var wo = woa.GetWorldObject();
                            if (wo != null)
                            {
                                AddToInventoryFiltered(wo, minable);
                            }
                        }
                    }
                }
            }
            else
            {
                Log("- Minerals [disabled]");
            }

            Log("Done");

            void AddToInventoryFiltered(WorldObject wo, Actionnable ag)
            {
                Log("  Check for " + wo.GetId() + " (" + wo.GetGroup().id + ")");
                var group = wo.GetGroup();
                var grid = group.id;
                if (includeGroupIds.Count == 0)
                {
                    if (excludeGroupIds.Contains(grid))
                    {
                        Log("    Excluded via ExcludeList");
                        return;
                    }
                }
                else if (!includeGroupIds.Contains(grid))
                {
                    Log("    Excluded via IncludeList");
                    return;
                }
                
                if (IsLarvae(grid) && !grabLarvae.Value)
                {
                    Log("    Grabbing larvae is disabled");
                    return;
                }
                else if (IsFishEggs(grid) && !grabFishEggs.Value)
                {
                    Log("    Grabbing fish eggs is disabled");
                    return;
                }
                else if (IsFrogEggs(grid) && !grabFrogEggs.Value)
                {
                    Log("    Grabbing frog eggs is disabled");
                    return;
                }
                else if (IsFood(grid) && !grabFood.Value)
                {
                    Log("    Grabbing food is disabled");
                    return;
                }
                else if (IsAlgae(grid) && !grabAlgae.Value)
                {
                    Log("    Grabbing algae is disabled");
                    return;
                }
                else if (IsGrabableOre(grid) && !mineMinerals.Value)
                {
                    Log("    Grabbing ore is disabled");
                    return;
                }
                else if (IsRod(grid) && !grabRods.Value)
                {
                    Log("    Grabbing rods is disabled");
                    return;
                }

                // ---

                if (ag != null && !IsGrabTarget(grid))
                {
                    Log("    Not a grab target");
                    return;
                }
                if (ag != null && GrabChecker.IsOnDisplay(ag))
                {
                    Log("    Is being displayed");
                    return;
                }

                // FIXME: grabbed: true  ????
                InventoriesHandler.Instance.AddWorldObjectToInventory(wo, backpackInv, grabbed: false, success =>
                {
                    if (success)
                    {
                        Log("    Grabbing success");

                        Managers.GetManager<DisplayersHandler>()
                        ?.GetInformationsDisplayer()
                        ?.AddInformation(2f, Readable.GetGroupName(group) + " + 1  (" + CountInInventory(backpackInv, group) + ")",
                            DataConfig.UiInformationsType.InInventory, group.GetImage());

                        ac.GetPlayerAudio().PlayGrab();
                        Managers.GetManager<DisplayersHandler>()
                            ?.GetItemWorldDisplayer()
                            ?.Hide();

                        if (ag != null && ag is ActionGrabable agr)
                        {
                            var onGrab = agr.grabedEvent;
                            agr.grabedEvent = null;
                            if (onGrab != null)
                            {
                                Log("    Invoking grabedEvent");
                                onGrab.Invoke(wo);
                                Log("    Invoking grabedEvent done.");
                            }
                        }
                    }
                });
            }
        }

        static bool IsLarvae(string grid)
        {
            return grid.StartsWith("LarvaeBase") || (grid.StartsWith("Butterfly") && grid.EndsWith("Larvae"));
        }

        static bool IsFrogEggs(string grid)
        {
            return grid.StartsWith("Frog") && grid.EndsWith("Eggs");
        }

        static bool IsFishEggs(string grid)
        {
            return grid.StartsWith("Fish") && grid.EndsWith("Eggs");
        }

        static bool IsFood(string grid)
        {
            return (grid.StartsWith("Vegetable") && grid.EndsWith("Growable"))
                || (grid.StartsWith("Cook") && grid.EndsWith("Growable"));
        }

        static bool IsAlgae(string grid)
        {
            return grid.StartsWith("Algae") && grid.EndsWith("Seed");
        }

        static bool IsGrabTarget(string grid)
        {
            return IsLarvae(grid) || IsFishEggs(grid) || IsFrogEggs(grid)
                || IsFood(grid) || IsAlgae(grid) || IsGrabableOre(grid)
                || IsRod(grid);
        }

        static int CountInInventory(Inventory inv, Group gr)
        {
            int count = 0;
            foreach (var wo in inv.GetInsideWorldObjects())
            {
                if (wo.GetGroup() == gr)
                {
                    count++;
                }
            }
            return count;
        }

        static bool IsGrabableOre(string grid)
        {
            return StandardResourceSets.defaultOreSet.Contains(grid);
        }

        static bool IsRod(string grid)
        {
            return grid.StartsWith("Rod-");
        }
    }
}
