using BepInEx;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using System;
using BepInEx.Bootstrap;

namespace UIShowRocketCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowrocketcount", "(UI) Show Rocket Counts", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid
         = "akarnokd.theplanetcraftermods.featmultiplayer";

        static FieldInfo associatedGroup;
        static ConfigEntry<int> fontSize;
        static ManualLogSource logger;

        static Func<string, int> multiplayerGetCount;

        static readonly Dictionary<string, int> rocketCountsByGroupId = new();
        static readonly Dictionary<DataConfig.WorldUnitType, int> rocketCountsByUnitType = new();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            associatedGroup = AccessTools.Field(typeof(EventHoverShowGroup), "associatedGroup");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size of the counter text on the craft screen");

            logger = Logger;

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out BepInEx.PluginInfo pi))
            {
                 multiplayerGetCount = GetApi<Func<string, int>>(pi, "apiCountByGroupId");
                Logger.LogInfo("Found " + modFeatMultiplayerGuid + ", using its group counter");
            }
            else
            {
                Logger.LogInfo("Not found " + modFeatMultiplayerGuid + ", counting rockets ourselves");
            }

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static T GetApi<T>(BepInEx.PluginInfo pi, string name)
        {
            return (T)AccessTools.Field(pi.Instance.GetType(), name)?.GetValue(null);
        }

        static bool TryGetCountByGroupId(string groupId, out int c)
        {
            if (multiplayerGetCount != null)
            {
                c = multiplayerGetCount(groupId);
                return true;
            }
            return rocketCountsByGroupId.TryGetValue(groupId, out c);
        }

        static bool TryGetCountByUnitType(DataConfig.WorldUnitType unitType, out int c)
        {
            if (multiplayerGetCount != null)
            {
                foreach (Group g in GroupsHandler.GetAllGroups())
                {
                    if (g is GroupItem gi && gi.GetGroupUnitMultiplier(unitType) != 0f)
                    {
                        string gid = gi.GetId();
                        if (gid.StartsWith("Rocket") && gid != "RocketReactor")
                        {
                            c = multiplayerGetCount(gid);
                            return true;
                        }
                    }
                }
                c = 0;
                return false;
            }
            return rocketCountsByUnitType.TryGetValue(unitType, out c);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnitsGenerationDisplayer), "RefreshDisplay")]
        static bool WorldUnitsGenerationDisplayer_RefreshDisplay(ref IEnumerator __result, float timeRepeat,
            WorldUnitsHandler ___worldUnitsHandler, List<TextMeshProUGUI> ___textFields, List<DataConfig.WorldUnitType> ___correspondingUnit)
        {
            __result = RefreshDisplayEnumerator(timeRepeat, ___worldUnitsHandler, ___textFields, ___correspondingUnit);
            return false;
        }

        static IEnumerator RefreshDisplayEnumerator(float timeRepeat, WorldUnitsHandler worldUnitsHandler, 
            List<TextMeshProUGUI> textFields, List<DataConfig.WorldUnitType> correspondingUnit)
        {
            for (; ; )
            {
                if (worldUnitsHandler != null)
                {
                    for (int idx = 0; idx < textFields.Count; idx++)
                    {
                        WorldUnit unit = worldUnitsHandler.GetUnit(correspondingUnit[idx]);
                        if (unit != null)
                        {
                            string s = unit.GetDisplayStringForValue(unit.GetCurrentValuePersSec(), false, 0) + "/s";
                            TryGetCountByUnitType(unit.GetUnitType(), out int c);
                            if (c > 0)
                            {
                                s = c + " x -----    " + s;
                            }
                            textFields[idx].text = s;
                        }
                    }
                }
                yield return new WaitForSeconds(timeRepeat);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowCraft), "CreateGrid")]
        static void UiWindowCraft_CreateGrid(GridLayoutGroup ___grid)
        {
            int fs = fontSize.Value;

            foreach (Transform tr in ___grid.transform)
            {
                EventHoverShowGroup ehg = tr.gameObject.GetComponent<EventHoverShowGroup>();
                if (ehg != null)
                {
                    Group g = associatedGroup.GetValue(ehg) as Group;
                    if (g != null)
                    {
                        TryGetCountByGroupId(g.GetId(), out int c);
                        if (c > 0)
                        {
                            GameObject go = new GameObject();
                            Transform parent = tr.gameObject.transform;
                            go.transform.parent = parent;

                            Text text = go.AddComponent<Text>();
                            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                            text.text = c + " x";
                            text.color = new Color(1f, 1f, 1f, 1f);
                            text.fontSize = fs;
                            text.resizeTextForBestFit = false;
                            text.verticalOverflow = VerticalWrapMode.Truncate;
                            text.horizontalOverflow = HorizontalWrapMode.Overflow;
                            text.alignment = TextAnchor.MiddleCenter;

                            Vector2 v = tr.gameObject.GetComponent<Image>().GetComponent<RectTransform>().sizeDelta;

                            RectTransform rectTransform = text.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(0, v.y / 2 + 10, 0);
                            rectTransform.sizeDelta = new Vector2(fs * 3, fs + 5);
                        }
                    }
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            rocketCountsByGroupId.Clear();
            rocketCountsByUnitType.Clear();

            if (multiplayerGetCount == null)
            {
                logger.LogInfo("Counting rockets");
                foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
                {
                    if (wo.GetGroup() is GroupItem gi)
                    {
                        string gid = gi.GetId();
                        if (gid.StartsWith("Rocket") && gid != "RocketReactor")
                        {
                            UpdateCount(gid, gi);
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionSendInSpace), "HandleRocketMultiplier")]
        static void ActionSendInSpace_HandleRocketMultiplier(WorldObject _worldObjectToSend)
        {
            if (multiplayerGetCount == null)
            {
                if (_worldObjectToSend.GetGroup() is GroupItem gi)
                {
                    UpdateCount(gi.GetId(), gi);
                }
            }
        }

        static void UpdateCount(string gid, GroupItem gi)
        {
            rocketCountsByGroupId.TryGetValue(gid, out var c);
            rocketCountsByGroupId[gid] = c + 1;

            foreach (var ids in GameConfig.spaceGlobalMultipliersGroupIds)
            {
                if (gi.GetGroupUnitMultiplier(ids.Key) != 0f)
                {
                    rocketCountsByUnitType.TryGetValue(ids.Key, out c);
                    rocketCountsByUnitType[ids.Key] = c + 1;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            rocketCountsByGroupId.Clear();
            rocketCountsByUnitType.Clear();
        }
    }
}
