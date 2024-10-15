// Copyright (c) 2022-2024, David Karnok & Contributors
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
using System.Linq;

namespace UIOverviewPanel
{
    [BepInPlugin(modUiOverviewPanelGuid, "(UI) Overview Panel", PluginInfo.PLUGIN_VERSION)]
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
        static readonly Dictionary<string, int> sceneCounts = [];
        static readonly HashSet<string> uniqueButterflies = [];
        static readonly HashSet<string> uniqueFish = [];
        static readonly HashSet<string> uniqueFrog = [];

        static Coroutine statisticsUpdater;

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

                AddTextRow("Mode", CreateMode());

                AddTextRow("", () => "");


                AddTextRow("Power", CreateEnergyProduction());
                AddTextRow("- (demand)", CreateEnergyDemand());
                AddTextRow("- (excess)", CreateEnergyExcess());

                AddTextRow("", () => "");

                AddTextRow("Oxygen", CreateWorldUnitCurrentValue(DataConfig.WorldUnitType.Oxygen));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Oxygen));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Oxygen));

                AddTextRow("Heat", CreateWorldUnitCurrentValue(DataConfig.WorldUnitType.Heat));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Heat));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Heat));

                AddTextRow("Pressure", CreateWorldUnitCurrentValue(DataConfig.WorldUnitType.Pressure));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Pressure));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Pressure));

                AddTextRow("Biomass", CreateWorldUnitCurrentValue(DataConfig.WorldUnitType.Biomass));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Biomass));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Biomass));

                AddTextRow("Plants", CreateWorldUnitCurrentValue(DataConfig.WorldUnitType.Plants));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Plants));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Plants));

                AddTextRow("Insects", CreateWorldUnitCurrentValue(DataConfig.WorldUnitType.Insects));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Insects));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Insects));

                // AddTextRow("Animals", () => "Not implemented in the game");
                AddTextRow("Animals", CreateWorldUnitCurrentValue(DataConfig.WorldUnitType.Animals));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Animals));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Animals));

                AddTextRow("", () => "");

                AddTextRow("Next Ti stage", CreateNextTiStage());
                AddTextRow("- (growth)", CreateWorldUnitChangeValue(DataConfig.WorldUnitType.Terraformation));
                AddTextRow("- (next unlock at)", CreateWorldUnitUnlock(DataConfig.WorldUnitType.Terraformation));
                AddTextRow("- (next unlock item)", CreateWorldUnitUnlockItem(DataConfig.WorldUnitType.Terraformation));

                AddTextRow("", () => "");

                AddTextRow("Microchips unlocked", CreateMicrochipUnlock());

                var pd = Managers.GetManager<PlanetLoader>()?.GetPlanetData();

                if (pd != null && pd.id == "Humble")
                {
                    AddTextRow("Starform chests found", CreateIdCounter(
                        104938870, 108185956, 101338958, 106708099, 105547336,
                        108239106, 109729201, 102343794, 105829268, 106518600,
                        102636198, 104222068, 101449629, 108725859, 102829376,
                        109796680, 108708926, 108621657, 105301774, 109034442,
                        101606525, 109173923, 104503750
                    ));
                }
                else
                {
                    AddTextRow("Golden chests found", CreateSceneCounter(26, "GoldenContainer"));
                }

                AddTextRow("Unique larvae found", CreateButterflyCount());

                AddTextRow("Unique fish found", CreateFishCount());

                AddTextRow("Unique frog found", CreateFrogCount());

                AddTextRow("Trade Tokens", CreateTradeTokens());

                AddTextRow("Items crafted", CreateCraftedItems());

                AddTextRow("Resources mined", CreateSceneCounter(0,
                    [.. StandardResourceSets.defaultOreSet]
                ));

                backgroundRectTransform.sizeDelta = new Vector2(Screen.width / 4, Screen.height / 4); // we'll resize this later
            }
        }

        Func<string> CreateWorldUnitCurrentValue(DataConfig.WorldUnitType unitType)
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(unitType);

                return string.Format("{0:#,##0.00}    + {1:#,##0.00} /s", wut.GetValue(), wut.GetCurrentValuePersSec());
            };
        }

        Func<string> CreateWorldUnitChangeValue(DataConfig.WorldUnitType unitType)
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(unitType);

                return string.Format("{0:#,##0.00} {1}", wut.GetCurrentValuePersSec(), " /s");
            };
        }

        Func<string> CreateEnergyDemand()
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(DataConfig.WorldUnitType.Energy);

                return string.Format("{0:#,##0.00} {1}", Math.Abs(wut.GetDecreaseValuePersSec()), " /h");
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
                            return "Host";
                        }
                        return "Singleplayer";
                    }
                    return "Client";
                }
                return "Singleplayer";
            };
        }

        Func<string> CreateEnergyProduction()
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(DataConfig.WorldUnitType.Energy);

                return string.Format("{0:#,##0.00} {1}", wut.GetIncreaseValuePersSec(), " /h");
            };
        }

        Func<string> CreateEnergyExcess()
        {
            return () =>
            {
                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(DataConfig.WorldUnitType.Energy);

                return string.Format("{0:#,##0.00} {1}", wut.GetIncreaseValuePersSec() + wut.GetDecreaseValuePersSec(), " /h");
            };
        }

        Func<string> CreateTradeTokens()
        {
            return () =>
            {
                return string.Format("{0:#,##0} (Total acquired: {1:#,##0})", TokensHandler.Instance.GetTokensNumber(), TokensHandler.Instance.GetAllTimeTokensNumber());
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

                var prevUnlocks = unlock.GetUnlockableGroupsUnderUnit(unitType);
                var nextUnlocks = unlock.GetUnlockableGroupsOverUnit(unitType);

                var str = "[ " + prevUnlocks.Count + " / " + (prevUnlocks.Count + nextUnlocks.Count) + " ]";

                if (nextUnlocks.Count == 0)
                {
                    str += " < fully unlocked >";
                }
                else
                {
                    var prevValue = 0f;
                    if (prevUnlocks.Count != 0)
                    {
                        prevValue = prevUnlocks[^1].GetUnlockingInfos().GetUnlockingValue();
                    }

                    var nextUnlock = nextUnlocks[0];
                    var value = nextUnlock.GetUnlockingInfos().GetUnlockingValue();

                    var wu = Managers.GetManager<WorldUnitsHandler>();
                    var wut = wu.GetUnit(unitType);
                    var remaining = Mathf.InverseLerp(prevValue, value, wut.GetValue()) * 100;
                    var speed = wut.GetCurrentValuePersSec();
                    var gameSettings = Managers.GetManager<GameSettingsHandler>();
                    if (gameSettings != null)
                    {
                        speed *= gameSettings.GetComputedTerraformationMultiplayerFactor(unitType);
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

                    str += String.Format(" @ {0:#,##0} ({1:##0.00} %, ETA {2})", value, remaining, eta);
                }
                return str;
            };
        }
        Func<string> CreateWorldUnitUnlockItem(DataConfig.WorldUnitType unitType)
        {
            return () =>
            {
                UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

                var nextUnlocks = unlock.GetUnlockableGroupsOverUnit(unitType);

                var str = "";

                if (nextUnlocks.Count == 0)
                {
                    str += "N/A";
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

                if (next == null)
                {
                    return Readable.GetTerraformStageName(curr);
                }

                var wu = Managers.GetManager<WorldUnitsHandler>();
                var wut = wu.GetUnit(DataConfig.WorldUnitType.Terraformation);

                var cstart = curr.GetStageStartValue();
                var nstart = next.GetStageStartValue();
                var sperc = Mathf.InverseLerp(cstart, nstart, wut.GetValue());

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
                    string.Format("{0:#,##0} Ti ({1:##0.00} %, ETA {2})", nstart, sperc * 100, eta);
            };
        }

        Func<String> CreateIdCounter(params int[] ids)
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

        Func<string> CreateSceneCounter(int max, params string[] groupIds)
        {
            return () =>
            {
                int csum = 0;
                foreach (var gid in groupIds)
                {
                    sceneCounts.TryGetValue(gid, out var c);
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
                var gid = worldObject.GetGroup().GetId();
                if (WorldObjectsIdHandler.IsWorldObjectFromScene(id))
                {
                    sceneCounts.TryGetValue(gid, out var c);
                    sceneCounts[gid] = c + 1;
                }
                if (gid.StartsWith("Butterfly") && gid.EndsWith("Larvae"))
                {
                    uniqueButterflies.Add(gid);
                }
                if (gid.StartsWith("Fish") && gid.EndsWith("Eggs"))
                {
                    uniqueFish.Add(gid);
                }
                if (gid.StartsWith("Frog") && gid.EndsWith("Eggs"))
                {
                    uniqueFrog.Add(gid);
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
                ClearCounters();
                foreach (var wo in WorldObjectsHandler.Instance.GetAllWorldObjects())
                {
                    UpdateCounters(wo.Value);
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
        }

        void AddTextRow(string heading, Func<string> getValue)
        {
            int fs = fontSize.Value;

            OverviewEntry result = new()
            {
                getValue = getValue
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
            if (parent.activeSelf && t - lastUpdate >= 0.5f)
            {
                lastUpdate = t;

                float col1Max = 0f;
                float col2Max = 0f;
                float margin = 10f;
                float marginY = 2f;

                foreach (var e in entries)
                {
                    e.valueText.text = e.getValue();

                    col1Max = Math.Max(col1Max, e.headingText.preferredWidth);
                    col2Max = Math.Max(col2Max, e.valueText.preferredWidth);
                }

                float w = 3 * margin + col1Max + col2Max;

                float fs = fontSize.Value;
                float h = entries.Count * (fs + marginY) + 2 * margin;
                float y = h / 2 - margin - fs / 2;
                foreach (var e in entries)
                {
                    float hx = -w / 2 + margin + e.headingText.preferredWidth / 2;
                    e.headingTransform.localPosition = new Vector3(hx, y, 0);
                    float tx = -w / 2 + 2 * margin + col1Max + e.valueText.preferredWidth / 2;
                    e.valueTransform.localPosition = new Vector3(tx, y, 0);

                    y -= fs + marginY;
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
                if (g.id.StartsWith(prefix) && g.id.EndsWith(suffix))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
