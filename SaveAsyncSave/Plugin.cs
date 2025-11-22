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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace SaveAsyncSave
{
    [BepInPlugin("akarnokd.theplanetcraftermods.saveasyncsave", "(Save) Async Save", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ManualLogSource logger;

        static Plugin me;

        static ConfigEntry<bool> modEnabled;

        static ConfigEntry<bool> debugMode;

        static volatile int mainThreadId;

        static readonly object gate = new();

        static Task saveTask;

        static bool quitDelayOnce;

        public void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            me = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            modEnabled = Config.Bind("General", "Enabled", true, "Is this mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable detailed logging (chatty!)");

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
        [HarmonyPatch(typeof(JSONExport), nameof(JSONExport.SaveToJson))]
        static bool JSONExport_SaveToJson(
            string _saveFileName,
            JsonableWorldState _worldState,
            List<JsonablePlanetState> _planetState,
            List<JsonablePlayerState> _playerState,
            JsonablePlayerStats _playerStats,
            JsonableGameState _gameSate,
            List<JsonableWorldObject> _worldObjects,
            List<JsonableInventory> _inventories,
            List<JsonableMessage> _messages,
            List<JsonableStoryEvent> _storyEvents,
            List<JsonableTerrainLayer> _terrainLayers,
            List<JsonableProceduralInstance> _proceduralInstances,
            ref Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
            if (!modEnabled.Value)
            {
                return true;
            }
            Log("Main thread check: " + Thread.CurrentThread.ManagedThreadId);
            if (mainThreadId == Thread.CurrentThread.ManagedThreadId)
            {
                var worldObjectCopyRef = worldObjectCopy;
                var inventoryCopyRef = inventoryCopy;
                if (worldObjectCopyRef.Count == 0 || inventoryCopyRef.Count == 0)
                {
                    logger.LogError("Exception: Oops, almost corrupted the save. " + worldObjectCopyRef.Count + " : " + inventoryCopyRef.Count
                        + "\r\nat " + Environment.StackTrace
                        );
                    return false;
                }
                Log("Starting background save routine");
                saveTask = Task.Factory.StartNew(() =>
                {
                    lock (gate)
                    {
                        var sw = Stopwatch.StartNew();
                        if (worldObjectCopyRef.Count == 0 || inventoryCopyRef.Count == 0)
                        {
                            logger.LogError("Exception: Oops, almost corrupted the save. " + worldObjectCopyRef.Count + " : " + inventoryCopyRef.Count
                                + "\r\nat " + Environment.StackTrace
                                );
                            return;
                        }
                        _worldObjects.Capacity = worldObjectCopyRef.Capacity + 1;
                        foreach (var item in worldObjectCopyRef)
                        {
                            _worldObjects.Add(item.ToJsonable());
                        }
                        var el = sw.Elapsed.TotalMilliseconds;
                        Log("Convert to JsonableWorldObject took " + el + " - " + (el / Math.Max(1, _worldObjects.Count)));
                        sw.Restart();
                        _inventories.Capacity = inventoryCopyRef.Capacity + 1;
                        foreach (var item in inventoryCopyRef)
                        {
                            _inventories.Add(item.ToJsonable());
                        }
                        el = sw.Elapsed.TotalMilliseconds;
                        Log("Convert to JsonableInventory took " + el + " - " + (el / Math.Max(1, _inventories.Count)));

                        try
                        {
                            JSONExport.SaveToJson(
                                _saveFileName,
                                _worldState,
                                _planetState,
                                _playerState,
                                _playerStats,
                                _gameSate,
                                _worldObjects,
                                _inventories,
                                _messages,
                                _storyEvents,
                                _terrainLayers,
                                _proceduralInstances
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex);
                        }
                    }
                });
                saveTask.ContinueWith(t =>
                {
                    Log("SaveTask status: " + t.Status);
                    if (t.IsFaulted)
                    {
                        Log(t.Exception.ToString());
                    }
                });
                return false;
            }
            Log("SaveToJson on background thread");
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(JSONExport), nameof(JSONExport.SaveToJson))]
        static void JSONExport_SaveToJson_Post(Stopwatch __state)
        {
            Log(Thread.CurrentThread.ManagedThreadId + ": " + __state.Elapsed.TotalMilliseconds);
        }

        static List<WorldObjectCopy> worldObjectCopy = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetWorldObjects")]
        static bool SavedDataHandler_SetAndGetWorldObjects(
            ref List<JsonableWorldObject> __result,
            ref Stopwatch __state)
        {
            __state = Stopwatch.StartNew();
            if (!modEnabled.Value)
            {
                worldObjectCopy.Clear();
                return true;
            }

            var worldObjects = WorldObjectsHandler.Instance.GetAllWorldObjects().Values;

            __result = new(/* worldObjects.Count + 1 */);
            if (saveTask == null || saveTask.IsCompleted)
            {
                worldObjectCopy.Clear();
            }
            else
            {
                worldObjectCopy = [];
            }
            int cap = worldObjects.Count + 1;
            if (worldObjectCopy.Capacity < cap)
            {
                worldObjectCopy.Capacity = cap;
            }

            foreach (var wo in worldObjects)
            {
                if (!wo.GetDontSaveMe())
                {
                    // __result.Add(JsonablesHelper.WorldObjectToJsonable(wo));
                    worldObjectCopy.Add(new WorldObjectCopy(wo));
                }
            }
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetWorldObjects")]
        static void SavedDataHandler_SetAndGetWorldObjects_Post(
            ref Stopwatch __state,
            ref List<JsonableWorldObject> __result)
        {
            var el = __state.Elapsed.TotalMilliseconds;
            int n = worldObjectCopy.Count + __result.Count;
            Log("SavedDataHandler_SetAndGetWorldObjects: " + n + " in " + el + " - " + el / Math.Max(1, n));
        }

        static List<InventoryCopy> inventoryCopy = [];

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetInventories")]
        static bool SavedDataHandler_SetAndGetInventories(
            ref Stopwatch __state,
            ref List<JsonableInventory> __result
        )
        {
            __state = Stopwatch.StartNew();
            if (!modEnabled.Value)
            {
                inventoryCopy.Clear();
                return true;
            }

            __result = new();
            if (saveTask == null || saveTask.IsCompleted)
            {
                inventoryCopy.Clear();
            }
            else
            {
                inventoryCopy = [];
            }
            var allInventory = InventoriesHandler.Instance.GetAllInventories().Values;
            int cap = allInventory.Count;
            if (inventoryCopy.Capacity < cap)
            {
                inventoryCopy.Capacity = cap;
            }

            foreach (var inv in allInventory)
            {
                if (inv.GetId() >= 0)
                {
                    inventoryCopy.Add(new InventoryCopy(inv));
                }
            }

            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavedDataHandler), "SetAndGetInventories")]
        static void SavedDataHandler_SetAndGetInventories_Post(
            ref Stopwatch __state,
            ref List<JsonableInventory> __result)
        {
            var el = __state.Elapsed.TotalMilliseconds;
            int n = __result.Count + inventoryCopy.Count;
            Log("SavedDataHandler_SetAndGetInventories: " + n + " in " + el + " - " + el / Math.Max(1, n));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Intro), "Start")]
        static void Intro_Start()
        {
            Application.wantsToQuit += WantsToQuit;
        }

        static bool WantsToQuit()
        {
            if (saveTask == null || saveTask.IsCompleted)
            {
                quitDelayOnce = false;
                return true;
            }
            Log("Can't just quit now.");
            if (!quitDelayOnce)
            {
                quitDelayOnce = true;
                me.StartCoroutine(SaveWaiter());
            }
            return false;
        }

        static IEnumerator SaveWaiter()
        {
            int n = 0;
            yield return new WaitForSecondsRealtime(1);
            while (saveTask != null && !saveTask.IsCompleted)
            {
                Log("Waiting for the save to complete...");
                if (++n == 1)
                {
                    MainMenuMessage.ShowDialog("Saving in progress...\n\nGame will close afterwards.");
                }
                yield return new WaitForSecondsRealtime(1);
            }
            yield return new WaitForEndOfFrame();
            quitDelayOnce = false;
            Log("Quitting.");
            Application.Quit();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineAutoCrafter), "SetItemsInRange")]
        static bool MachineAutoCrafter_SetItemsInRange()
        {
            return !quitDelayOnce;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventorySpawnContent), "UpdateComponent")]
        static bool InventorySpawnContent_UpdateComponent()
        {
            return !quitDelayOnce;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkManager), "OnApplicationQuit")]
        static bool NetworkManager_OnApplicationQuit()
        {
            // Log(Environment.StackTrace);
            return !quitDelayOnce;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NetworkUtils), "OnDestroy")]
        static bool NetworkUtils_OnDestroy()
        {
            return !quitDelayOnce;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            Log("UiWindowPause_OnQuit(): " + (saveTask == null || saveTask.IsCompleted));

            if (saveTask == null || saveTask.IsCompleted)
            {
                worldObjectCopy.Clear();
                inventoryCopy.Clear();
            }
            else
            {
                worldObjectCopy = [];
                inventoryCopy = [];
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnSave))]
        static void UiWindowPause_OnSave(UiWindowPause __instance, Selectable ___saveButton)
        {
            var qb = ___saveButton.gameObject.transform.parent.Find("ButtonQuit").GetComponent<Button>();
            if (Managers.GetManager<SavedDataHandler>().IsSaving())
            {
                Log("Disabling Quit Button interaction.");
                qb.interactable = false;
                __instance.StartCoroutine(QuitButtonEnabler(qb));
            }
        }
        static IEnumerator QuitButtonEnabler(Selectable quitButton)
        {
            while (Managers.GetManager<SavedDataHandler>().IsSaving())
            {
                yield return null;
            }
            quitButton.interactable = true;
            Log("Enabling Quit Button interaction.");
        }
    }

    internal class WorldObjectCopy
    {
        internal int id;
        internal string groupId;
        internal int linkedInventoryId;
        internal List<int> secondaryInventories;
        internal List<Group> linkedGroups;
        internal Vector3 position;
        internal Quaternion rotation;
        internal int planetHash;
        internal int planetLinkedHash;
        internal Color color;
        internal string text;
        internal List<int> panelIds;
        internal int growth;
        internal int setting;
        internal int traitType;
        internal int traitValue;
        internal float hunger;
        internal Vector2Int count;
        internal int linkedWo;
        internal WorldObjectCopy(WorldObject worldObject)
        {
            id = worldObject.GetId();
            groupId = worldObject.GetGroup().id;
            linkedInventoryId = worldObject.GetLinkedInventoryId();
            secondaryInventories = Copy(worldObject.GetSecondaryInventoriesId());
            linkedGroups = Copy(worldObject.GetLinkedGroups());
            position = worldObject.GetPosition();
            rotation = worldObject.GetRotation();
            planetHash = worldObject.GetPlanetHash();
            planetLinkedHash = worldObject.GetPlanetLinkedHash();
            color = worldObject.GetColor();
            text = worldObject.GetText();
            panelIds = Copy(worldObject.GetPanelsId());
            growth = Mathf.RoundToInt(worldObject.GetGrowth());
            setting = worldObject.GetSetting();
            traitType = (int)worldObject.GetGeneticTraitType();
            traitValue = worldObject.GetGeneticTraitValue();
            hunger = worldObject.GetHunger();
            count = worldObject.GetCount();
            linkedWo = worldObject.GetLinkedWorldObject();
        }

        internal JsonableWorldObject ToJsonable()
        {
            return new JsonableWorldObject(
                id, groupId, linkedInventoryId,
                DataTreatments.IntListToString(secondaryInventories),
                GroupsHandler.GetGroupsStringIds(linkedGroups),
                DataTreatments.Vector3ToString(position),
                DataTreatments.QuaternionToString(rotation),
                planetHash,
                planetLinkedHash,
                DataTreatments.ColorToString(color),
                text,
                DataTreatments.IntListToString(panelIds),
                growth, setting,
                traitType,
                traitValue,
                hunger,
                DataTreatments.Vector2IntToString(count),
                linkedWo
                );
        }

        List<int> Copy(List<int> source)
        {
            if (source == null || source.Count == 0)
            {
                return EmptyInt;
            }
            return new(source);
        }
        List<Group> Copy(List<Group> source)
        {
            if (source == null || source.Count == 0)
            {
                return EmptyGroup;
            }
            return new(source);
        }

        static readonly List<int> EmptyInt = [];
        static readonly List<Group> EmptyGroup = [];
    }

    internal class InventoryCopy
    {
        internal int id;
        internal List<WorldObject> content;
        internal int size;
        internal HashSet<Group> demandGroups;
        internal HashSet<Group> supplyGroups;
        internal int priority;

        internal InventoryCopy(Inventory inventory)
        {
            id = inventory.GetId();
            content = new(inventory.GetInsideWorldObjects());
            size = inventory.GetSize();
            LogisticEntity logisticEntity = inventory.GetLogisticEntity();
            demandGroups = Copy(logisticEntity.GetDemandGroups());
            supplyGroups = Copy(logisticEntity.GetSupplyGroups());
            priority = logisticEntity.GetPriority();
        }

        internal JsonableInventory ToJsonable()
        {
            var text = new StringBuilder(content.Count * 10 + 1);

            foreach (var wo in content)
            {
                text.Append(wo.GetId());
                text.Append(',');
            }
            if (content.Count != 0)
            {
                text.Length--;
            }
            return new JsonableInventory(
                id,
                text.ToString(),
                size,
                GroupsHandler.GetGroupsStringIds(demandGroups),
                GroupsHandler.GetGroupsStringIds(supplyGroups),
                priority
            );

        }

        HashSet<Group> Copy(HashSet<Group> source)
        {
            if (source == null || source.Count == 0)
            {
                return EmptyGroup;
            }
            return new(source);
        }

        static readonly HashSet<Group> EmptyGroup = [];
    }

}
