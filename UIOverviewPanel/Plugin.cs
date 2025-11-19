// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;
using BepInEx.Configuration;
using System;
using Unity.Netcode;
using System.Collections;
using static SpaceCraft.DataConfig;
using LibCommon;

namespace UIOverviewPanel
{
    [BepInPlugin(modUiOverviewPanelGuid, "(UI) Overview Panel", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("akarnokd.theplanetcraftermods.uitranslationhungarian", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modUiOverviewPanelGuid = "akarnokd.theplanetcraftermods.uioverviewpanel";

        static Plugin me;

        static ConfigEntry<int> fontSize;
        static ConfigEntry<string> key;
        static ConfigEntry<int> updateFrequency;

        static ManualLogSource logger;

        static Color defaultBackgroundColor = new(0.25f, 0.25f, 0.25f, 0.9f);
        static Color defaultTextColor = new(1f, 1f, 1f, 1f);

        static GameObject parent;
        static RectTransform backgroundRectTransform;
        static readonly List<OverviewEntry> entries = [];
        static float lastUpdate;
        /*
        static readonly Dictionary<string, int> sceneCounts = [];
        static readonly HashSet<string> uniqueButterflies = [];
        static readonly HashSet<string> uniqueFish = [];
        static readonly HashSet<string> uniqueFrog = [];
        */
        static readonly DictionaryCounter sceneCounts = new(1024);
        static readonly HashSetFast uniqueButterflies = new(64);
        static readonly HashSetFast uniqueFish = new(64);
        static readonly HashSetFast uniqueFrog = new(64);

        static Coroutine statisticsUpdater;

        static readonly RollingWindowUpdater systemTiUpdater = new();
        const float systemTiHorizon = 10f;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;
            me = this;

            fontSize = Config.Bind("General", "FontSize", 16, "Font size");
            key = Config.Bind("General", "Key", "F1", "The keyboard key to toggle the panel (no modifiers)");
            updateFrequency = Config.Bind("General", "UpdateFrequency", 7, "How often to update the item statistics, in seconds");

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            var h = Harmony.CreateAndPatchAll(typeof(Plugin));
            LibCommon.ModPlanetLoaded.Patch(h, modUiOverviewPanelGuid, _ => PlanetLoader_HandleDataAfterLoad());

            Logger.LogInfo($"Plugin patches applied!");
        }

        void Update()
        {
            PlayersManager playersManager = Managers.GetManager<PlayersManager>();
            if (playersManager != null)
            {
                PlayerMainController player = playersManager.GetActivePlayerController();
                if (player != null)
                {
                    Setup();
                    UpdateRender();
                    return;
                }
            }
        }

        void Setup()
        {
            if (parent == null)
            {
                logger.LogInfo("Begin Creating the Overview Panel");
                parent = new GameObject("OverviewPanelCanvas");
                parent.SetActive(false); // off by default
                Canvas canvas = parent.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var background = new GameObject("OverviewPanelCanvas-Background");
                background.transform.parent = parent.transform;
                Image image = background.AddComponent<Image>();
                image.color = defaultBackgroundColor;

                backgroundRectTransform = image.GetComponent<RectTransform>();
                backgroundRectTransform.localPosition = new Vector3(0, 0, 0);

                entries.Clear();

                AddTextRow(Translate("OverviewPanel_Mode"), CreateMode());
                AddTextRow(Translate("OverviewPanel_Planet"), CreatePlanet());

                AddTextRow("", () => "");


                AddTextRow(Translate("OverviewPanel_Power"), CreateEnergyProduction());

                AddTextRow("", () => "");

                AddTextRow(Translate("OverviewPanel_Oxygen"), CreateWorldUnitCurrentValue(WorldUnitType.Oxygen));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Oxygen));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Oxygen));

                AddTextRow(Translate("OverviewPanel_Heat"), CreateWorldUnitCurrentValue(WorldUnitType.Heat));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Heat));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Heat));

                AddTextRow(Translate("OverviewPanel_Pressure"), CreateWorldUnitCurrentValue(WorldUnitType.Pressure));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Pressure));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Pressure));

                AddTextRow(Translate("OverviewPanel_Biomass"), CreateWorldUnitCurrentValue(WorldUnitType.Biomass));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Biomass));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Biomass));

                AddTextRow(Translate("OverviewPanel_Plants"), CreateWorldUnitCurrentValue(WorldUnitType.Plants));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Plants));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Plants));

                AddTextRow(Translate("OverviewPanel_Insects"), CreateWorldUnitCurrentValue(WorldUnitType.Insects));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Insects));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Insects));

                AddTextRow(Translate("OverviewPanel_Animals"), CreateWorldUnitCurrentValue(WorldUnitType.Animals));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Animals));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Animals));

                AddTextRow(Translate("OverviewPanel_Purity"), CreateWorldUnitCurrentValue(WorldUnitType.Purification), PurityVisible());
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Purification), PurityVisible());
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Purification), PurityVisible());

                AddTextRow("", () => "");

                AddTextRow(Translate("OverviewPanel_NextTIStage"), CreateNextTiStage());
                AddTextRow(Translate("OverviewPanel_Growth"), CreateWorldUnitChangeValue(WorldUnitType.Terraformation));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.Terraformation));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.Terraformation));

                AddTextRow("", () => "");

                AddTextRow(Translate("OverviewPanel_NextSysTIStage"), CreateNextSysTiStage());
                AddTextRow(Translate("OverviewPanel_Growth"), CreateWorldUnitChangeValue(WorldUnitType.SystemTerraformation));
                AddTextRow(Translate("OverviewPanel_NextUnlockAt"), CreateWorldUnitUnlock(WorldUnitType.SystemTerraformation));
                AddTextRow(Translate("OverviewPanel_NextUnlockItem"), CreateWorldUnitUnlockItem(WorldUnitType.SystemTerraformation));

                AddTextRow("", () => "");

                AddTextRow(Translate("OverviewPanel_MicrochipsUnlocked"), CreateMicrochipUnlock());

                AddTextRow(Translate("OverviewPanel_ChestsFound"), CreateChestsFound());

                AddTextRow(Translate("OverviewPanel_UniqueLarvaeFound"), CreateButterflyCount());

                AddTextRow(Translate("OverviewPanel_UniqueFishFound"), CreateFishCount());

                AddTextRow(Translate("OverviewPanel_UniqueFrogFound"), CreateFrogCount());

                AddTextRow(Translate("OverviewPanel_TradeTokens"), CreateTradeTokens());

                AddTextRow(Translate("OverviewPanel_ItemsCrafted"), CreateCraftedItems());

                AddTextRow(Translate("OverviewPanel_ResourcesMined"), CreateSceneCounter(0,
                    [.. StandardResourceSets.defaultOreSet]
                ));

                backgroundRectTransform.sizeDelta = new Vector2(Screen.width / 4, Screen.height / 4); // we'll resize this later

                systemTiUpdater.Clear();
            }
        }

        Func<bool> PurityVisible()
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                return wu.GetUnit(WorldUnitType.Purification) is WorldUnitPurification pur && pur.GetValue() >= 0;
            };
        }

        Func<string> CreateWorldUnitCurrentValue(DataConfig.WorldUnitType unitType)
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(unitType);

                return string.Format("{0:#,##0.00}    + {1:#,##0.00} {2}", 
                    wut.GetValue(), wut.GetCurrentValuePersSec(), 
                    Translate("OverviewPanel_PerSecond"));
            };
        }

        Func<string> CreateWorldUnitChangeValue(DataConfig.WorldUnitType unitType)
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(unitType);
                var speed = (double)wut.GetCurrentValuePersSec();
                if (unitType == WorldUnitType.SystemTerraformation)
                {
                    speed = systemTiUpdater.CalculateSpeed();
                }
                if (speed > 1e15)
                {
                    return string.Format("{0:0.000000000e+0} {1}", speed,
                        Translate("OverviewPanel_PerSecond"));
                }
                return string.Format("{0:#,##0.00} {1}", speed,
                    Translate("OverviewPanel_PerSecond"));
            };
        }

        Func<string> CreateMode()
        {
            return () =>
            {
                if (NetworkManager.Singleton != null)
                {
                    if (NetworkManager.Singleton.IsServer)
                    {
                        if ((Managers.GetManager<PlayersManager>()?.GetAllTimeListOfPlayers().Count ?? 1) > 1)
                        {
                            return Translate("OverviewPanel_Host");
                        }
                        return Translate("OverviewPanel_Singleplayer");
                    }
                    return Translate("OverviewPanel_Client");
                }
                return Translate("OverviewPanel_Singleplayer");
            };
        }

        Func<string> CreatePlanet()
        {
            return () =>
            {
                var pd = Managers.GetManager<PlanetLoader>()?.GetCurrentPlanetData();
                return Translate("Planet_" + pd?.id ?? "Unknown");
            };
        }

        Func<string> CreateEnergyProduction()
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(DataConfig.WorldUnitType.Energy);

                var excess = wut.GetIncreaseValuePersSec() + wut.GetDecreaseValuePersSec();
                var demand = Math.Abs(wut.GetDecreaseValuePersSec());

                var maxStr = "";
                if (wu.GetUnit(WorldUnitType.Purification) is WorldUnitPurification pur && pur.GetValue() >= 0)
                {
                    var ea = pur.GetEnergyAvailable();
                    if (ea > 1E9)
                    {
                        maxStr = string.Format("{0} {1:0.000e+0} {2}",
                            Translate("OverviewPanel_Max"),
                            ea,
                            Translate("OverviewPanel_PerHour")
                        );
                    }
                    else
                    {
                        maxStr = string.Format("{0} {1:#,##0.00} {2}",
                            Translate("OverviewPanel_Max"),
                            ea,
                            Translate("OverviewPanel_PerHour")
                        );
                    }
                }

                return string.Format(
                    "{0:#,##0.00} {1} = {2:#,##0.00} {1} {3} {4:#,##0.00} {5} {1} {6}",
                    wut.GetIncreaseValuePersSec(),
                    Translate("OverviewPanel_PerHour"),
                    demand,
                    excess < 0 ? " - <color=#FF8080>" : " + <color=#80FF80>",
                    Math.Abs(excess),
                    "</color>",
                    maxStr
                );
            };
        }

        Func<string> CreateTradeTokens()
        {
            return () =>
            {
                return string.Format("{0:#,##0} ({2}: {1:#,##0})", 
                    TokensHandler.Instance.GetTokensNumber(), 
                    TokensHandler.Instance.GetAllTimeTokensNumber(),
                    Translate("OverviewPanel_TotalAcquired"));
            };
        }

        Func<string> CreateMicrochipUnlock()
        {
            return () =>
            {
                UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

                List<List<GroupData>> tiers =
                [
                    unlock.unlockingData.tier1GroupToUnlock,
                    unlock.unlockingData.tier2GroupToUnlock,
                    unlock.unlockingData.tier3GroupToUnlock,
                    unlock.unlockingData.tier4GroupToUnlock,
                    unlock.unlockingData.tier5GroupToUnlock,
                    unlock.unlockingData.tier6GroupToUnlock,
                    unlock.unlockingData.tier7GroupToUnlock,
                    unlock.unlockingData.tier8GroupToUnlock,
                    unlock.unlockingData.tier9GroupToUnlock,
                    unlock.unlockingData.tier10GroupToUnlock,
                ];

                HashSet<string> unlockedIds = [];

                foreach (var g in UnlockedGroupsHandler.Instance.GetUnlockedGroups())
                {
                    unlockedIds.Add(g.GetId());
                }

                int unlocked = 0;
                int total = 0;



                foreach (var list in tiers)
                {
                    foreach (var e in list)
                    {
                        if (unlockedIds.Contains(e.id))
                        {
                            unlocked++;
                        }
                        total++;
                    }
                }

                return unlocked + " / " + total + string.Format(" ({0:##0.00} %)", 100f * unlocked / total);
            };
        }

        Func<string> CreateWorldUnitUnlock(DataConfig.WorldUnitType unitType)
        {
            return () =>
            {
                UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

                var prevUnlocks = unlock.GetUnlockableGroupsUnderUnit(unitType, unitType != WorldUnitType.SystemTerraformation);
                var nextUnlocks = unlock.GetUnlockableGroupsOverUnit(unitType, unitType != WorldUnitType.SystemTerraformation);

                var str = "[ " + prevUnlocks.Count + " / " + (prevUnlocks.Count + nextUnlocks.Count) + " ]";

                if (nextUnlocks.Count == 0)
                {
                    str += Translate("OverviewPanel_FullyUnlocked");
                }
                else
                {
                    // FIXME units sometimes double now!!!
                    var prevValue = 0d;
                    if (prevUnlocks.Count != 0)
                    {
                        prevValue = prevUnlocks[^1].GetUnlockingInfos().GetUnlockingValue();
                    }

                    var nextUnlock = nextUnlocks[0];
                    var value = nextUnlock.GetUnlockingInfos().GetUnlockingValue();

                    var wu = Managers.GetManager<WorldUnitsHandler>();
                    var wut = wu.GetUnit(unitType);
                    var remaining = 0d;
                    if (prevValue != value)
                    {
                        remaining = (wut.GetValue() - prevValue) / (value - prevValue);
                        if (remaining < 0d)
                        {
                            remaining = 0d;
                        }
                        else if (remaining > 1d)
                        {
                            remaining = 1d;
                        }
                    }
                    remaining *= 100;
                    var speed = (double)wut.GetCurrentValuePersSec();
                    if (unitType == WorldUnitType.SystemTerraformation)
                    {
                        speed = systemTiUpdater.CalculateSpeed();
                    }
                    var gameSettings = Managers.GetManager<GameSettingsHandler>();
                    if (gameSettings != null)
                    {
                        speed *= gameSettings.GetComputedTerraformationMultiplayerFactor(unitType);
                    }

                    var eta = "\u221E";
                    if (speed > 0)
                    {
                        var t = (value - wut.GetValue()) / speed;
                        if (t > 365d * 24 * 60 * 60)
                        {
                            eta = Translate("OverviewPanel_YearPlus");
                        }
                        else if (t >= 60d * 60)
                        {
                            eta = string.Format("{0}:{1:00}:{2:00}", (int)(t) / 60 / 60, ((int)(t) / 60) % 60, (int)t % 60);
                        }
                        else
                        {
                            eta = string.Format("{0}:{1:00}", (int)(t) / 60, (int)(t) % 60);
                        }
                    }
                    if (value > 1e15)
                    {
                        str += string.Format(" @ {0:0.000000000e+0} ({1:##0.00} %, {3} {2})", value, remaining, eta, Translate("OverviewPanel_ETA"));
                    }
                    else {
                        str += string.Format(" @ {0:#,##0} ({1:##0.00} %, {3} {2})", value, remaining, eta, Translate("OverviewPanel_ETA"));
                    }
                }
                return str;
            };
        }
        Func<string> CreateWorldUnitUnlockItem(DataConfig.WorldUnitType unitType)
        {
            return () =>
            {
                UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

                var nextUnlocks = unlock.GetUnlockableGroupsOverUnit(unitType, unitType != WorldUnitType.SystemTerraformation);

                var str = "";

                if (nextUnlocks.Count == 0)
                {
                    str += Translate("OverviewPanel_NA");
                }
                else
                {
                    var nextUnlock = nextUnlocks[0];

                    str += Readable.GetGroupName(nextUnlock);
                }
                return str;
            };
        }

        Func<string> CreateNextTiStage()
        {
            return () =>
            {
                var terraformStages = Managers.GetManager<TerraformStagesHandler>();

                var curr = terraformStages.GetCurrentGlobalStage();
                var next = terraformStages.GetNextGlobalStage();

                if (next == null || next == curr)
                {
                    return Readable.GetTerraformStageName(curr);
                }

                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(DataConfig.WorldUnitType.Terraformation);

                var cstart = (float)curr.GetStageStartValue(); // FIXME units now double in some API
                var nstart = (float)next.GetStageStartValue();
                var sperc = Mathf.InverseLerp(cstart, nstart, (float)wut.GetValue());

                var value = nstart;
                var speed = wut.GetCurrentValuePersSec();
                var gameSettings = Managers.GetManager<GameSettingsHandler>();
                if (gameSettings != null)
                {
                    speed *= gameSettings.GetComputedTerraformationMultiplayerFactor(DataConfig.WorldUnitType.Terraformation);
                }


                var eta = "\u221E";
                if (speed > 0)
                {
                    var t = (value - wut.GetValue()) / speed;
                    if (t >= 60 * 60)
                    {
                        eta = string.Format("{0}:{1:00}:{2:00}", (int)(t) / 60 / 60, ((int)(t) / 60) % 60, (int)t % 60);
                    }
                    else
                    {
                        eta = string.Format("{0}:{1:00}", (int)(t) / 60, (int)(t) % 60);
                    }
                }

                return Readable.GetTerraformStageName(next) + " @ " + 
                    string.Format("{0:#,##0} Ti ({1:##0.00} %, {3} {2})", nstart, sperc * 100, eta, Translate("OverviewPanel_ETA"));
            };
        }

        Func<string> CreateNextSysTiStage()
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(DataConfig.WorldUnitType.SystemTerraformation);
                var v = wut.GetValue();
                if (v > 1E15)
                {
                    return string.Format("{0:0.000000000e+0} SysTi", wut.GetValue());
                }
                return string.Format("{0:#,##0} SysTi", wut.GetValue());
            };
        }

        double GetCurrentSysTi()
        {
            var wu = Managers.GetManager<WorldUnitsHandler>();
            if (wu == null)
            {
                return 0d;
            }
            var wut = wu.GetUnit(DataConfig.WorldUnitType.SystemTerraformation);
            if (wut == null)
            {
                return 0d;
            }
            return wut.GetValue();
        }

        Func<string> CreateIdCounter(params int[] ids)
        {
            return () =>
            {
                int csum = 0;
                foreach (var id in ids) {
                    var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(id);
                    var inv = InventoriesHandler.Instance.GetInventoryById(id);
                    if (wo != null && inv != null && inv.GetInsideWorldObjects().Count == 0)
                    {
                        csum++;
                    };
                }
                return csum + " / " + ids.Length + " (" + string.Format("{0:##0.00}", 100f * csum / ids.Length) + " %)";
            };
        }

        Func<string> CreateChestsFound()
        {
            var starform = CreateIdCounter(
                        101338958, 101449629, 101606525, 101609984, 102343794,
                        102636198, 102829376, 104222068, 104503750, 105301774,
                        105547336, 105829268, 106518600, 106708099, 107463079,
                        107716309, 108185956, 108239106, 108621657, 108708926,
                        108725859, 109034442, 109173923, 109729201, 109796680
                   );
            //var golden = CreateSceneCounter(28, "GoldenContainer");
            var golden = CreateIdCounter(
                 106014910, 103762341, 105674853, 103811904, 108243927,
                 106960229, 105811267, 107183786, 104405814, 103248374,
                 105308637, 101293320, 102281766, 103931496, 105502699,
                 106352839, 106354955, 104275552, 108228777, 105145505,
                 109405033, 109147878, 103884010, 103854789, 109614299,
                 106785344, 101896178, 106518009
                );
            var clam = CreateIdCounter(
                105660430, 105901098, 101457828, 106751818, 106998888,
                104169290, 101166643, 108177977, 102931249, 103579240,
                101629406
            );
            return () =>
            {
                return Translate("OverviewPanel_ChestsFound_Golden") + golden()
                + " | " + Translate("OverviewPanel_ChestsFound_Starform") + starform()
                + " | " + Translate("OverviewPanel_ChestsFound_Clam") + clam();
            };
        }

        Func<string> CreateSceneCounter(int max, params string[] groupIds)
        {
            return () =>
            {
                int csum = 0;
                foreach (var gid in groupIds)
                {
                    //sceneCounts.TryGetValue(gid, out var c);
                    var c = sceneCounts.CountOf(gid);
                    csum += c;
                }

                if (max > 0)
                {
                    return csum + " / " + max + " (" + string.Format("{0:##0.00}", 100f * csum / max) + " %)";
                }
                return string.Format("{0:#,##0}", csum);
            };
        }

        Func<string> CreateButterflyCount()
        {
            int max = CountGroups("Butterfly", "Larvae");
            return () =>
            {
                int csum = uniqueButterflies.Count;
                return csum + " / " + max + " (" + string.Format("{0:##0.00}", 100f * csum / max) + " %)";
            };
        }
        Func<string> CreateFishCount()
        {
            int max = CountGroups("Fish", "Eggs");
            return () =>
            {
                int csum = uniqueFish.Count;
                return csum + " / " + max + " (" + string.Format("{0:##0.00}", 100f * csum / max) + " %)";
            };
        }

        Func<string> CreateFrogCount()
        {
            int max = CountGroups("Frog", "Eggs");
            return () =>
            {
                int csum = uniqueFrog.Count;
                return csum + " / " + max + " (" + string.Format("{0:##0.00}", 100f * csum / max) + " %)";
            };
        }

        Func<string> CreateCraftedItems()
        {
            return () =>
            {
                return string.Format("{0:#,##0}", CraftManager.GetTotalCraftedObjects());
            };
        }

        static void UpdateCounters(WorldObject worldObject)
        {
            if (!worldObject.GetDontSaveMe())
            {
                /*
                logger.LogInfo(worldObject.GetId() + ", " + worldObject.GetGroup().GetId() + ", " + worldObject.GetPosition());
                logger.LogInfo(Environment.StackTrace);
                */

                var id = worldObject.GetId();
                var gid = worldObject.GetGroup().id;
                if (WorldObjectsIdHandler.IsWorldObjectFromScene(id))
                {
                    /*
                    sceneCounts.TryGetValue(gid, out var c);
                    sceneCounts[gid] = c + 1;
                    */
                    sceneCounts.Update(gid);
                }
                if (gid.StartsWith("Butterfly", StringComparison.Ordinal) 
                    && gid.EndsWith("Larvae", StringComparison.Ordinal))
                {
                    uniqueButterflies.Add(gid);
                }
                else
                if (gid.EndsWith("Eggs", StringComparison.Ordinal))
                {
                    if (gid.StartsWith("Fish", StringComparison.Ordinal))
                    {
                        uniqueFish.Add(gid);
                    }
                    else
                    if (gid.StartsWith("Frog", StringComparison.Ordinal))
                    {
                        uniqueFrog.Add(gid);
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.SetAllWorldObjects))]
        static void WorldObjectsHandler_SetAllWorldObjects(List<WorldObject> allWorldObjects)
        {
            ClearCounters();
            foreach (WorldObject wo in allWorldObjects)
            {
                UpdateCounters(wo);
            }
        }

        static void PlanetLoader_HandleDataAfterLoad()
        {
            if (statisticsUpdater != null)
            {
                me.StopCoroutine(statisticsUpdater);
                statisticsUpdater = null;
            }
            statisticsUpdater = me.StartCoroutine(PeriodicStatistics());
        }

        static IEnumerator PeriodicStatistics()
        {
            var wait = new WaitForSeconds(updateFrequency.Value);

            while (WorldObjectsHandler.Instance != null)
            {
                if (parent != null && parent.activeSelf)
                {
                    ClearCounters();
                    foreach (var wo in WorldObjectsHandler.Instance.GetAllWorldObjects())
                    {
                        UpdateCounters(wo.Value);
                    }
                }
                yield return wait;
            }
        }

        static void ClearCounters()
        {
            sceneCounts.Clear();
            uniqueButterflies.Clear();
            uniqueFish.Clear();
            uniqueFrog.Clear();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            ClearCounters();
            if (statisticsUpdater != null)
            {
                me.StopCoroutine(statisticsUpdater);
                statisticsUpdater = null;
            }
            systemTiUpdater.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }

        class OverviewEntry
        {
            internal Text headingText;
            internal RectTransform headingTransform;
            internal Text valueText;
            internal RectTransform valueTransform;
            internal Func<string> getValue;
            internal Func<bool> isVisible;
        }

        void AddTextRow(string heading, Func<string> getValue, Func<bool> isVisible = null)
        {
            int fs = fontSize.Value;

            OverviewEntry result = new()
            {
                getValue = getValue,
                isVisible = isVisible
            };

            var hg = new GameObject("OverviewPanelCanvas-Heading-" + heading);
            hg.transform.SetParent(parent.transform);

            CreateText(heading, hg, fs, out result.headingText, out result.headingTransform);

            var vg = new GameObject("OverviewPanelCanvas-Value-" + heading);
            vg.transform.SetParent(parent.transform);

            CreateText("", vg, fs, out result.valueText, out result.valueTransform);

            entries.Add(result);
        }

        void CreateText(string str, GameObject go, int fs, out Text text, out RectTransform transform)
        {
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = str;
            txt.color = defaultTextColor;
            txt.fontSize = fs;
            txt.resizeTextForBestFit = false;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.supportRichText = true;

            var rectTransform = go.GetComponent<RectTransform>();
            rectTransform.localPosition = new Vector3(0, 0, 0);
            rectTransform.sizeDelta = new Vector2(txt.preferredWidth, fs);

            text = txt;
            transform = rectTransform;
        }

        void UpdateRender()
        {
            FieldInfo pi = typeof(Key).GetField(key.Value.ToString().ToUpper());
            Key k = Key.F1;
            if (pi != null)
            {
                k = (Key)pi.GetRawConstantValue();
            }
            WindowsHandler wh = Managers.GetManager<WindowsHandler>();
            if (Keyboard.current[k].wasPressedThisFrame && wh != null && !wh.GetHasUiOpen())
            {
                parent.SetActive(!parent.activeSelf);
            }

            float t = Time.time;

            systemTiUpdater.Update(Time.time, GetCurrentSysTi(), systemTiHorizon);

            if (parent.activeSelf && t - lastUpdate >= 0.5f)
            {
                lastUpdate = t;

                float col1Max = 0f;
                float col2Max = 0f;
                float margin = 10f;
                float marginY = 2f;

                foreach (var e in entries)
                {
                    if (e.isVisible?.Invoke() ?? true)
                    {
                        e.valueText.text = e.getValue();

                        col1Max = Math.Max(col1Max, e.headingText.preferredWidth);
                        col2Max = Math.Max(col2Max, e.valueText.preferredWidth);
                    }
                }

                float w = 3 * margin + col1Max + col2Max;

                float fs = fontSize.Value;
                float h = entries.Count * (fs + marginY) + 2 * margin;
                float y = h / 2 - margin - fs / 2;
                foreach (var e in entries)
                {
                    if (e.isVisible?.Invoke() ?? true)
                    {
                        e.headingTransform.gameObject.SetActive(true);
                        e.valueTransform.gameObject.SetActive(true);

                        float hx = -w / 2 + margin + e.headingText.preferredWidth / 2;
                        e.headingTransform.localPosition = new Vector3(hx, y, 0);
                        float tx = -w / 2 + 2 * margin + col1Max + e.valueText.preferredWidth / 2;
                        e.valueTransform.localPosition = new Vector3(tx, y, 0);

                        y -= fs + marginY;
                    }
                    else
                    {
                        e.headingTransform.gameObject.SetActive(false);
                        e.valueTransform.gameObject.SetActive(false);
                    }
                }

                backgroundRectTransform.sizeDelta = new Vector2(w, h);
            }
        }

        int CountGroups(string prefix, string suffix)
        {
            var grps = GroupsHandler.GetAllGroups();
            int count = 0;

            foreach (var g in grps)
            {
                if (g.id.StartsWith(prefix, StringComparison.Ordinal) && g.id.EndsWith(suffix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), "LoadLocalization")]
        static void Localization_LoadLocalization(
        Dictionary<string, Dictionary<string, string>> ___localizationDictionary
)
        {
            if (___localizationDictionary.TryGetValue("hungarian", out var dict))
            {
                dict["OverviewPanel_Mode"] = "Játékmód";
                dict["OverviewPanel_Planet"] = "Bolygó";
                dict["OverviewPanel_Power"] = "Energia";
                dict["OverviewPanel_Power_Demand"] = "- (igény)";
                dict["OverviewPanel_Power_Excess"] = "- (többlet)";
                dict["OverviewPanel_Oxygen"] = "Oxigén";
                dict["OverviewPanel_NextUnlockAt"] = "- (következő feloldás érték)";
                dict["OverviewPanel_NextUnlockItem"] = "- (következő feloldandó tárgy)";
                dict["OverviewPanel_Heat"] = "Hő";
                dict["OverviewPanel_Pressure"] = "Nyomás";
                dict["OverviewPanel_Biomass"] = "Biomassza";
                dict["OverviewPanel_Plants"] = "Növények";
                dict["OverviewPanel_Insects"] = "Rovarok";
                dict["OverviewPanel_Animals"] = "Állatok";
                dict["OverviewPanel_Purity"] = "Tisztaság";
                dict["OverviewPanel_NextTIStage"] = "Következő TI szakasz";
                dict["OverviewPanel_NextSysTIStage"] = "Rendszer TI";
                dict["OverviewPanel_Growth"] = "- (növekedés)";
                dict["OverviewPanel_MicrochipsUnlocked"] = "Mikrocsipek feloldva";
                dict["OverviewPanel_StarformChestsFound"] = "Starform láda megtalálva";
                dict["OverviewPanel_GoldenChestsFound"] = "Aranyláda megtalálva";
                dict["OverviewPanel_UniqueLarvaeFound"] = "Egyedi lepke lárva megtalálva";
                dict["OverviewPanel_ChestsFound"] = "Láda megtalálva";
                dict["OverviewPanel_ChestsFound_Golden"] = "<color=#FFCC00>Arany:</color> ";
                dict["OverviewPanel_ChestsFound_Starform"] = "<color=#CCFFCC>Starform:</color> ";
                dict["OverviewPanel_ChestsFound_Clam"] = "<color=#FFCC00>Kagyló:</color> ";
                dict["OverviewPanel_UniqueFishFound"] = "Egyedi halikra megtalálva";
                dict["OverviewPanel_UniqueFrogFound"] = "Egyedi békalárva megtalálva";
                dict["OverviewPanel_TradeTokens"] = "Kereskedelmi tokenek";
                dict["OverviewPanel_ItemsCrafted"] = "Tárgy létrehozva";
                dict["OverviewPanel_ResourcesMined"] = "Nyersanyag bányászva";
                dict["OverviewPanel_PerSecond"] = "/mp";
                dict["OverviewPanel_PerMinute"] = "/perc";
                dict["OverviewPanel_PerHour"] = "/óra";
                dict["OverviewPanel_ETA"] = "Hátravan";
                dict["OverviewPanel_Host"] = "Házigazda";
                dict["OverviewPanel_Singleplayer"] = "Egyjátékos";
                dict["OverviewPanel_Client"] = "Vendég";
                dict["OverviewPanel_TotalAcquired"] = "Összesen beszerzett";
                dict["OverviewPanel_FullyUnlocked"] = " < teljesen feloldva >";
                dict["OverviewPanel_NA"] = "N/A";
                dict["OverviewPanel_YearPlus"] = "Év+";
                dict["OverviewPanel_Max"] = " - Maximum: ";
            }
            if (___localizationDictionary.TryGetValue("english", out dict))
            {
                dict["OverviewPanel_Mode"] = "Game mode";
                dict["OverviewPanel_Planet"] = "Planet";
                dict["OverviewPanel_Power"] = "Power";
                dict["OverviewPanel_Power_Demand"] = "- (demand)";
                dict["OverviewPanel_Power_Excess"] = "- (excess)";
                dict["OverviewPanel_Oxygen"] = "Oxygen";
                dict["OverviewPanel_NextUnlockAt"] = "- (next unlock at)";
                dict["OverviewPanel_NextUnlockItem"] = "- (next unlock item)";
                dict["OverviewPanel_Heat"] = "Heat";
                dict["OverviewPanel_Pressure"] = "Pressure";
                dict["OverviewPanel_Biomass"] = "Biomass";
                dict["OverviewPanel_Plants"] = "Plants";
                dict["OverviewPanel_Insects"] = "Insects";
                dict["OverviewPanel_Animals"] = "Animals";
                dict["OverviewPanel_Purity"] = "Purification";
                dict["OverviewPanel_NextTIStage"] = "Next TI Stage";
                dict["OverviewPanel_NextSysTIStage"] = "System TI";
                dict["OverviewPanel_Growth"] = "- (growth)";
                dict["OverviewPanel_MicrochipsUnlocked"] = "Microchips unlocked";
                dict["OverviewPanel_StarformChestsFound"] = "Starform chests found";
                dict["OverviewPanel_GoldenChestsFound"] = "Golden chests found";
                dict["OverviewPanel_ChestsFound"] = "Chests found";
                dict["OverviewPanel_ChestsFound_Golden"] = "<color=#FFCC00>Golden:</color> ";
                dict["OverviewPanel_ChestsFound_Starform"] = "<color=#CCFFCC>Starform:</color> ";
                dict["OverviewPanel_ChestsFound_Clam"] = "<color=#FFCC00>Clam:</color> ";
                dict["OverviewPanel_UniqueLarvaeFound"] = "Unique larvae found";
                dict["OverviewPanel_UniqueFishFound"] = "Unique fish found";
                dict["OverviewPanel_UniqueFrogFound"] = "Unique frog found";
                dict["OverviewPanel_TradeTokens"] = "Trade tokens";
                dict["OverviewPanel_ItemsCrafted"] = "Items crafted";
                dict["OverviewPanel_ResourcesMined"] = "Resources mined";
                dict["OverviewPanel_PerSecond"] = "/s";
                dict["OverviewPanel_PerHour"] = "/h";
                dict["OverviewPanel_ETA"] = "ETA";
                dict["OverviewPanel_Host"] = "Host";
                dict["OverviewPanel_Singleplayer"] = "Singleplayer";
                dict["OverviewPanel_Client"] = "Client";
                dict["OverviewPanel_TotalAcquired"] = "Total acquired";
                dict["OverviewPanel_FullyUnlocked"] = " < fully unlocked >";
                dict["OverviewPanel_NA"] = "N/A";
                dict["OverviewPanel_YearPlus"] = "Year+";
                dict["OverviewPanel_Max"] = " - Maximum: ";
            }
            if (___localizationDictionary.TryGetValue("russian", out dict))
            {
                dict["OverviewPanel_Mode"] = "Режим";
                dict["OverviewPanel_Planet"] = "Планета";
                dict["OverviewPanel_Power"] = "Энергия";
                dict["OverviewPanel_Power_Demand"] = "- (требуется)";
                dict["OverviewPanel_Power_Excess"] = "- (доступно)";
                dict["OverviewPanel_Oxygen"] = "Кислород";
                dict["OverviewPanel_NextUnlockAt"] = "- (следующее открытие)";
                dict["OverviewPanel_NextUnlockItem"] = "- (откроется)";
                dict["OverviewPanel_Heat"] = "Тепло";
                dict["OverviewPanel_Pressure"] = "Давление";
                dict["OverviewPanel_Biomass"] = "Биомасса";
                dict["OverviewPanel_Plants"] = "Растения";
                dict["OverviewPanel_Insects"] = "Насекомые";
                dict["OverviewPanel_Animals"] = "Животные";
                dict["OverviewPanel_Purity"] = "Очищение";
                dict["OverviewPanel_NextTIStage"] = "Следующий этап";
                dict["OverviewPanel_NextSysTIStage"] = "Системная терраформация";
                dict["OverviewPanel_Growth"] = "- (рост)";
                dict["OverviewPanel_MicrochipsUnlocked"] = "Микрочипов расшифровано";
                dict["OverviewPanel_StarformChestsFound"] = "Starform найдено коробок";
                dict["OverviewPanel_GoldenChestsFound"] = "Найдено золотых ящиков";
                dict["OverviewPanel_ChestsFound"] = "Сундуков найдено";
                dict["OverviewPanel_ChestsFound_Golden"] = "<color=#FFCC00>Золотой:</color> ";
                dict["OverviewPanel_ChestsFound_Starform"] = "<color=#CCFFCC>Starform:</color> ";
                dict["OverviewPanel_ChestsFound_Clam"] = "<color=#FFCC00>Моллюск:</color> ";
                dict["OverviewPanel_UniqueLarvaeFound"] = "Найдено уникальных личинок";
                dict["OverviewPanel_UniqueFishFound"] = "Найдено уникальных рыб";
                dict["OverviewPanel_UniqueFrogFound"] = "Найдено уникальных лягушек";
                dict["OverviewPanel_TradeTokens"] = "Заработано токенов";
                dict["OverviewPanel_ItemsCrafted"] = "Создано предметов";
                dict["OverviewPanel_ResourcesMined"] = "Ресурсов добыто";
                dict["OverviewPanel_PerSecond"] = "/сек.";
                dict["OverviewPanel_PerHour"] = "кВт";
                dict["OverviewPanel_ETA"] = "ост.";
                dict["OverviewPanel_Host"] = "Хост";
                dict["OverviewPanel_Singleplayer"] = "Одиночная";
                dict["OverviewPanel_Client"] = "Клиент";
                dict["OverviewPanel_TotalAcquired"] = "Всего нажито";
                dict["OverviewPanel_FullyUnlocked"] = " < полностью разблокирован >";
                dict["OverviewPanel_NA"] = "нет в наличии";
                dict["OverviewPanel_YearPlus"] = "Год+";
                dict["OverviewPanel_Max"] = " - Максимум: ";
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Localization), nameof(Localization.SetLangage))]
        static void Localization_SetLanguage()
        {
            Destroy(parent);
            parent = null;
        }

        static string Translate(string code)
        {
            return Localization.GetLocalizedString(code);
        }

        internal class RollingWindowUpdater
        {
            readonly List<(float, double)> samples = [];

            internal void Update(float time, double value, float horizon)
            {
                samples.Add((time, value));

                var min = time - horizon;
                int j = samples.Count;
                for (int i = 0; i < samples.Count; i++)
                {
                    var s = samples[i];
                    if (s.Item1 >= min)
                    {
                        j = i;
                        break;
                    }
                }
                if (j > 0)
                {
                    samples.RemoveRange(0, j);
                }
            }

            internal double CalculateSpeed()
            {
                if (samples.Count != 0)
                {
                    var first = samples[0];
                    var last = samples[^1];
                    var dt = last.Item1 - first.Item1;
                    if (dt > 0)
                    {
                        return (last.Item2 - first.Item2) / dt;
                    }
                }
                return 0f;
            }

            internal void Clear()
            {
                samples.Clear();
            }
        }
    }
}
