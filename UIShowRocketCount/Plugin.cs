// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static MonoMod.Cil.RuntimeILReferenceBag.FastDelegateInvokers;

namespace UIShowRocketCount
{
    [BepInPlugin(modUiShowRocketCountGuid, "(UI) Show Rocket Counts", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string modUiShowRocketCountGuid = "akarnokd.theplanetcraftermods.uishowrocketcount";

        static AccessTools.FieldRef<EventHoverShowGroup, Group> fEventHoverShowGroupAssociatedGroup;
        static ConfigEntry<int> fontSize;
        static ConfigEntry<bool> debugMode;
        static ManualLogSource logger;

        static Font font;

        static readonly Dictionary<(int planetHash, string groupId), (Inventory inventory, bool missing)> rocketCache = [];

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            fEventHoverShowGroupAssociatedGroup = AccessTools.FieldRefAccess<EventHoverShowGroup, Group>("_associatedGroup");

            fontSize = Config.Bind("General", "FontSize", 20, "The font size of the counter text on the craft screen");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debug logging. Chatty!");

            font = Resources.GetBuiltinResource<Font>("Arial.ttf");


            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void Log(string message)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(message);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldUnitsGenerationDisplayer), "RefreshDisplay")]
        static bool WorldUnitsGenerationDisplayer_RefreshDisplay(
            WorldUnitsGenerationDisplayer __instance,
            ref IEnumerator __result, 
            float timeRepeat,
            List<TextMeshProUGUI> ___textFields, 
            List<DataConfig.WorldUnitType> ___correspondingUnit)
        {
            __result = RefreshDisplayEnumerator(timeRepeat, __instance, ___textFields, ___correspondingUnit);
            return false;
        }

        static void GetRocketInventory(Group group, int planetHash, System.Action<Inventory> callback)
        {
            if (rocketCache.TryGetValue((planetHash, group.id), out var inventory))
            {
                callback(inventory.inventory);
            }
            else
            {
                InventoriesHandler.Instance.GetInventoryFromFirstWorldObjectOfGroup(group, planetHash, call =>
                {
                    rocketCache[(planetHash, group.id)] = (call, call == null);
                    if (call != null)
                    {
                        if (!(NetworkManager.Singleton?.IsServer ?? true))
                        {
                            InventoriesHandler.Instance.BeginInventoryWatch(call);
                        }
                    }
                    callback(call);
                });
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ConstructibleProxy), "SendInSpaceClientRpc")]
        static void ConstructibleProxy_SendInSpaceClientRpc()
        {
            rocketCache.Where(e => e.Value.missing)
                .Select(e => e.Key)
                .ToList()
                .ForEach(e => rocketCache.Remove(e));
        }

        static IEnumerator RefreshDisplayEnumerator(
            float timeRepeat,
            WorldUnitsGenerationDisplayer instance, 
            List<TextMeshProUGUI> textFields, 
            List<DataConfig.WorldUnitType> correspondingUnit)
        {
            WorldUnitsHandler worldUnitsHandler = null;
            for (; ; )
            {
                if (worldUnitsHandler == null)
                {
                    worldUnitsHandler = Managers.GetManager<WorldUnitsHandler>();
                    AccessTools.Field(typeof(WorldUnitsGenerationDisplayer), "worldUnitsHandler").SetValue(instance, worldUnitsHandler);
                }
                if (worldUnitsHandler != null && worldUnitsHandler.AreUnitsInited())
                {
                    var planetId = 0;
                    var pl = Managers.GetManager<PlanetLoader>();
                    if (pl != null)
                    {
                        var cp = pl.GetCurrentPlanetData();
                        if (cp != null)
                        {
                            planetId = cp.GetPlanetHash();
                        }
                    }
                    for (int idx = 0; idx < textFields.Count; idx++)
                    {
                        var unitType = correspondingUnit[idx];
                        var unit = worldUnitsHandler.GetUnit(unitType);
                        if (unit != null)
                        {
                            var s = unit.GetDisplayStringForValue(unit.GetCurrentValuePersSec(), false, 0) + "/s";
                            var textField = textFields[idx];
                            var found = false;
                            if (GameConfig.spaceGlobalMultipliersGroupIds.TryGetValue(unitType, out var spaceId))
                            {
                                Log("Found spaceId: " + spaceId + " for " + unitType);
                                var spaceGroup = GroupsHandler.GetGroupViaId(spaceId);
                                if (spaceGroup != null)
                                {
                                    Log("Found spaceGroup");
                                    found = true;
                                    GetRocketInventory(spaceGroup, planetId, inventory =>
                                    {
                                        Log("Got fist world object inventory: " + (inventory?.GetId() ?? -1));
                                        int c = 0;
                                        if (inventory != null)
                                        {
                                            foreach (var item in inventory.GetInsideWorldObjects())
                                            {
                                                if (item.GetGroup() is GroupItem gc 
                                                    && gc.GetGroupUnitMultiplier(unitType) > 0)
                                                {
                                                    c++;
                                                }
                                            }
                                        }
                                        Log("Counted " + c);
                                        if (c > 0)
                                        {
                                            s = c + " x -----    " + s;
                                        }
                                        textField.text = s;
                                    });
                                }
                            }
                            if (!found)
                            {
                                textField.text = s;
                            }
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
            var planetId = 0;
            var pl = Managers.GetManager<PlanetLoader>();
            if (pl != null)
            {
                var cp = pl.GetCurrentPlanetData();
                if (cp != null)
                {
                    planetId = cp.GetPlanetHash();
                }
            }
            int fs = fontSize.Value;

            foreach (Transform tr in ___grid.transform)
            {
                var parent = tr.gameObject.transform;
                var ehg = tr.gameObject.GetComponent<EventHoverShowGroup>();
                if (ehg != null)
                {
                    if (fEventHoverShowGroupAssociatedGroup(ehg) is GroupItem gi)
                    {
                        if (gi.id.StartsWith("Rocket", StringComparison.Ordinal) && !gi.id.StartsWith("RocketReactor", StringComparison.Ordinal))
                        {
                            Log("Modifying cell for " + gi.id);

                            var go = new GameObject();
                            go.transform.SetParent(parent, false);

                            var text = go.AddComponent<Text>();
                            text.font = font;
                            text.color = new Color(1f, 1f, 1f, 1f);
                            text.fontSize = fs;
                            text.resizeTextForBestFit = false;
                            text.verticalOverflow = VerticalWrapMode.Truncate;
                            text.horizontalOverflow = HorizontalWrapMode.Overflow;
                            text.alignment = TextAnchor.MiddleCenter;

                            Vector2 v = tr.gameObject.GetComponent<Image>().GetComponent<RectTransform>().sizeDelta;

                            var rectTransform = text.GetComponent<RectTransform>();
                            rectTransform.localPosition = new Vector3(0, v.y / 2 + 10, 0);
                            rectTransform.sizeDelta = new Vector2(fs * 3, fs + 5);

                            var found = false;

                            foreach (var unitSpaceId in GameConfig.spaceGlobalMultipliersGroupIds)
                            {
                                var unitType = unitSpaceId.Key;

                                if (gi.GetGroupUnitMultiplier(unitType) > 0)
                                {
                                    found = true;
                                    var spaceId = unitSpaceId.Value;
                                    var spaceGroup = GroupsHandler.GetGroupViaId(spaceId);
                                    Log("Found spaceId: " + spaceId + " for " + unitType);
                                    if (spaceGroup != null)
                                    {
                                        Log("Found spaceGroup");
                                        GetRocketInventory(spaceGroup, planetId, inventory =>
                                        {
                                            Log("Got fist world obejct inventory: " + (inventory?.GetId() ?? -1));
                                            int c = 0;
                                            if (inventory != null)
                                            {
                                                foreach (var item in inventory.GetInsideWorldObjects())
                                                {
                                                    if (item.GetGroup() == gi)
                                                    {
                                                        c++;
                                                    }
                                                }
                                            }
                                            Log("Counted " + c);
                                            if (c > 0)
                                            {
                                                text.text = c + " x";
                                            }
                                        });
                                    }
                                    break;
                                }
                            }

                            if (!found)
                            {
                                var c = WorldObjectsHandler.Instance.GetObjectInWorldObjectsCount(gi.GetGroupData(), false);
                                if (c > 0)
                                {
                                    text.text = c + " x";
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            if (InventoriesHandler.Instance != null)
            {
                if (!(NetworkManager.Singleton?.IsServer ?? true)) {
                    foreach (var (inventory, _) in rocketCache.Values)
                    {
                        if (inventory != null)
                        {
                            InventoriesHandler.Instance.StopInventoryWatch(inventory);
                        }
                    }
                }
            }
            rocketCache.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }

    }
}
