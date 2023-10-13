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

namespace UIOverviewPanel
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uioverviewpanel", "(UI) Overview Panel", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        static ConfigEntry<int> fontSize;
        static ConfigEntry<string> key;

        static ManualLogSource logger;

        static Color defaultBackgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.9f);
        static Color defaultTextColor = new Color(1f, 1f, 1f, 1f);

        static GameObject parent;
        static RectTransform backgroundRectTransform;
        static readonly List<OverviewEntry> entries = new();
        static float lastUpdate;
        static readonly Dictionary<string, int> sceneCounts = new();
        static readonly HashSet<string> uniqueButterflies = new();
        static readonly HashSet<string> uniqueFish = new();
        static readonly HashSet<string> uniqueFrog = new();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            fontSize = Config.Bind("General", "FontSize", 16, "Font size");
            key = Config.Bind("General", "Key", "F1", "The keyboard key to toggle the panel (no modifiers)");

            Harmony.CreateAndPatchAll(typeof(Plugin));

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

                GameObject background = new GameObject("OverviewPanelCanvas-Background");
                background.transform.parent = parent.transform;
                Image image = background.AddComponent<Image>();
                image.color = defaultBackgroundColor;

                backgroundRectTransform = image.GetComponent<RectTransform>();
                backgroundRectTransform.localPosition = new Vector3(0, 0, 0);

                entries.Clear();

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

                AddTextRow("Golden chests found", CreateSceneCounter(23, "GoldenContainer"));

                AddTextRow("Unique larvae found", CreateButterflyCount(19));

                AddTextRow("Unique fish found", CreateFishCount(13));

                AddTextRow("Unique frog found", CreateFrogCount(12));

                AddTextRow("Trade Tokens", CreateTradeTokens());

                AddTextRow("Resources mined", CreateSceneCounter(0, 
                    "Cobalt",
                    "Silicon",
                    "Iron",
                    "ice", // it is not capitalized in the game
                    "Magnesium",
                    "Titanium",
                    "Aluminium",
                    "Uranim", // it is misspelled in the game
                    "Iridium",
                    "Alloy",
                    "Zeolite",
                    "Osmium",
                    "Sulfur",
                    "PulsarQuartz"
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
                return string.Format("{0:#,##0} (Total acquired: {1:#,##0})", TokensHandler.GetTokensNumber(), TokensHandler.GetAllTimeTokensNumber());
            };
        }

        Func<string> CreateMicrochipUnlock()
        {
            return () =>
            {
                UnlockingHandler unlock = Managers.GetManager<UnlockingHandler>();

                List<List<GroupData>> tiers = new List<List<GroupData>>
                {
                    unlock.tier1GroupToUnlock,
                    unlock.tier2GroupToUnlock,
                    unlock.tier3GroupToUnlock,
                    unlock.tier4GroupToUnlock,
                    unlock.tier5GroupToUnlock,
                    unlock.tier6GroupToUnlock,
                    unlock.tier7GroupToUnlock,
                    unlock.tier8GroupToUnlock,
                    unlock.tier9GroupToUnlock,
                    unlock.tier10GroupToUnlock,
                };

                HashSet<string> unlockedIds = new();

                foreach (var g in GroupsHandler.GetUnlockedGroups())
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
                        prevValue = prevUnlocks[prevUnlocks.Count - 1].GetUnlockingInfos().GetUnlockingValue();
                    }

                    var nextUnlock = nextUnlocks[0];
                    var value = nextUnlock.GetUnlockingInfos().GetUnlockingValue();

                    var wu = Managers.GetManager<WorldUnitsHandler>();
                    var wut = wu.GetUnit(unitType);
                    var remaining = Mathf.InverseLerp(prevValue, value, wut.GetValue()) * 100;
                    var speed = wut.GetCurrentValuePersSec();

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

        Func<string> CreateButterflyCount(int max)
        {
            return () =>
            {
                int csum = uniqueButterflies.Count;
                return csum + " / " + max + " (" + string.Format("{0:##0.00}", 100f * csum / max) + " %)";
            };
        }
        Func<string> CreateFishCount(int max)
        {
            return () =>
            {
                int csum = uniqueFish.Count;
                return csum + " / " + max + " (" + string.Format("{0:##0.00}", 100f * csum / max) + " %)";
            };
        }

        Func<string> CreateFrogCount(int max)
        {
            return () =>
            {
                int csum = uniqueFrog.Count;
                return csum + " / " + max + " (" + string.Format("{0:##0.00}", 100f * csum / max) + " %)";
            };
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "StoreNewWorldObject")]
        static void WorldObjectsHandler_StoreNewWorldObject(WorldObject _worldObject)
        {
            var id = _worldObject.GetId();
            var gid = _worldObject.GetGroup().GetId();
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

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.SetAllWorldObjects))]
        static void WorldObjectsHandler_SetAllWorldObjects(List<WorldObject> _allWorldObjects)
        {
            foreach (WorldObject wo in _allWorldObjects)
            {
                WorldObjectsHandler_StoreNewWorldObject(wo);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            sceneCounts.Clear();
            uniqueButterflies.Clear();
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

            OverviewEntry result = new();
            result.getValue = getValue;

            GameObject hg = new GameObject("OverviewPanelCanvas-Heading-" + heading);
            hg.transform.SetParent(parent.transform);

            CreateText(heading, hg, fs, out result.headingText, out result.headingTransform);

            GameObject vg = new GameObject("OverviewPanelCanvas-Value-" + heading);
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
    }
}
