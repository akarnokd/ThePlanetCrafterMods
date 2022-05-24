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

namespace UIShowRocketCount
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowrocketcount", "(UI) Show Rocket Counts", "1.0.0.3")]
    public class Plugin : BaseUnityPlugin
    {
        static FieldInfo associatedGroup;
        static ConfigEntry<int> fontSize;
        static ManualLogSource logger;

        static Dictionary<DataConfig.WorldUnitType, Inventory> rocketInventory = new();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            associatedGroup = AccessTools.Field(typeof(EventHoverShowGroup), "associatedGroup");
            fontSize = Config.Bind("General", "FontSize", 20, "The font size of the counter text on the craft screen");

            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnitsGenerationDisplayer), "RefreshDisplay")]
        static bool WorldUnitsGenerationDisplayer_RefreshDisplay(ref IEnumerator __result, float timeRepeat,
            WorldUnitsHandler ___worldUnitsHandler, List<TextMeshProUGUI> ___textFields, List<DataConfig.WorldUnitType> ___correspondingUnit)
        {
            __result = RefreshDisplayEnumerator(timeRepeat, ___worldUnitsHandler, ___textFields, ___correspondingUnit);
            return false;
        }

        static void CountRockets(Dictionary<string, int> rockets)
        {
            foreach (WorldObject worldObject in WorldObjectsHandler.GetAllWorldObjects())
            {
                string gid = worldObject.GetGroup().GetId();

                if (gid.StartsWith("Rocket"))
                {
                    rockets.TryGetValue(gid, out int v);
                    rockets[gid] = v + 1;
                }
            }
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
                            if (rocketInventory.TryGetValue(unit.GetUnitType(), out var inv))
                            {
                                int c = inv.GetInsideWorldObjects().Count;
                                if (c > 0)
                                {
                                    s = c + " x -----    " + s;
                                }
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
            Dictionary<string, int> rockets = new Dictionary<string, int>();
            CountRockets(rockets);
            int fs = fontSize.Value;

            foreach (Transform tr in ___grid.transform)
            {
                EventHoverShowGroup ehg = tr.gameObject.GetComponent<EventHoverShowGroup>();
                if (ehg != null)
                {
                    Group g = associatedGroup.GetValue(ehg) as Group;
                    if (g != null)
                    {
                        if (rockets.TryGetValue(g.GetId(), out int c)) {
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
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            rocketInventory.Clear();

            // Make a reverse map for the hidden container's group id to the world unit type
            Dictionary<string, DataConfig.WorldUnitType> rocketIds = new();
            foreach (var ids in GameConfig.spaceGlobalMultipliersGroupIds)
            {
                rocketIds.Add(ids.Value, ids.Key);
            }

            // scan all constructed world objects
            foreach (WorldObject wo in WorldObjectsHandler.GetConstructedWorldObjects())
            {
                Group gr = wo.GetGroup();
                string gid = gr.GetId();
                // if the groupid matches, get its inventory
                if (rocketIds.TryGetValue(gid, out var wu))
                {
                    rocketInventory.Add(wu, InventoriesHandler.GetInventoryById(wo.GetLinkedInventoryId()));
                }
            }

            // find the uninstantiated hidden containers and instantiate them now
            foreach (var ids in GameConfig.spaceGlobalMultipliersGroupIds)
            {
                if (!rocketInventory.ContainsKey(ids.Key))
                {
                    Group groupViaId = GroupsHandler.GetGroupViaId(ids.Value);
                    var wo = WorldObjectsHandler.CreateNewWorldObject(groupViaId, 0);
                    wo.SetPositionAndRotation(GameConfig.spaceLocation, Quaternion.identity);
                    WorldObjectsHandler.InstantiateWorldObject(wo, false);

                    rocketInventory.Add(ids.Key, InventoriesHandler.GetInventoryById(wo.GetLinkedInventoryId()));
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            rocketInventory.Clear();
        }
    }
}
