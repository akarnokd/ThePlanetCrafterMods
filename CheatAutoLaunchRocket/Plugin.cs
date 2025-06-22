// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using Unity.Netcode;
using System;

namespace CheatAutoLaunchRocket
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautolaunchrocket", "(Cheat) Auto Launch Rockets", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        static ConfigEntry<bool> isEnabled;

        static ConfigEntry<bool> debugMode;

        static ManualLogSource logger;

        private void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();

            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debugging with detailed logs (chatty!).");

            logger = Logger;

            LibCommon.HarmonyIntegrityCheck.Check(typeof(Plugin));
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Actionnable), "Start")]
        static void Actionable_Start(Actionnable __instance)
        {
            if (__instance is ActionSendInSpace asis)
            {
                logger.LogInfo("AutoLaunchRocket registering with button at " + __instance.transform.position);
                __instance.StartCoroutine(LaunchCheck(1f, asis));    
            }
        }

        static readonly Dictionary<int, WorldObject> rockets = [];

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "StoreNewWorldObject")]
        static void WorldObjectsHandler_StoreNewWorldObject(WorldObject worldObject)
        {
            var gid = worldObject.GetGroup().GetId();
            if (gid.StartsWith("Rocket", StringComparison.Ordinal) && !gid.StartsWith("RocketReactor", StringComparison.Ordinal))
            {
                rockets[worldObject.GetId()] = worldObject;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DestroyWorldObject), [typeof(WorldObject), typeof(bool)])]
        static void WorldObjectsHandler_DestroyWorldObject(WorldObject worldObject)
        {
            if (worldObject != null)
            {
                int id = worldObject.GetId();
                if (!WorldObjectsIdHandler.IsWorldObjectFromScene(id))
                {
                    var gid = worldObject.GetGroup().GetId();
                    if (gid.StartsWith("Rocket", StringComparison.Ordinal) && !gid.StartsWith("RocketReactor", StringComparison.Ordinal))
                    {
                        rockets.Remove(id);
                    }
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DestroyWorldObject), [typeof(int), typeof(bool)])]
        static void WorldObjectsHandler_DestroyWorldObject(WorldObjectsHandler __instance, int woId)
        {
            var worldObject = __instance.GetWorldObjectViaId(woId);
            if (worldObject != null && !WorldObjectsIdHandler.IsWorldObjectFromScene(woId))
            {

                var gid = worldObject.GetGroup().GetId();
                if (gid.StartsWith("Rocket", StringComparison.Ordinal) && !gid.StartsWith("RocketReactor", StringComparison.Ordinal))
                {
                    rockets.Remove(woId);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            rockets.Clear();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlackScreen), nameof(BlackScreen.DisplayLogoStudio))]
        static void BlackScreen_DisplayLogoStudio()
        {
            UiWindowPause_OnQuit();
        }

        /// <summary>
        /// Marker class so the same rocket is not launched twice.
        /// </summary>
        class AutoLaunchDelay : MonoBehaviour
        {

        }

        static void Log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }

        static IEnumerator LaunchCheck(float delay, ActionSendInSpace parent)
        {
            for (; ; )
            {
                if (NetworkManager.Singleton?.IsServer ?? true)
                {
                    Log("Launch check for " + rockets.Count + " known rockets");
                    var pos = parent.transform.position;
                    List<int> toRemove = [];
                    foreach (var wo in rockets.Values)
                    {
                        Log("       Rocket: " + wo.GetId() + ", " + wo.GetGroup().GetId() + ", " + wo.GetPosition());
                        if (wo.GetIsPlaced())
                        {
                            var go = wo.GetGameObject();
                            if (go != null)
                            {
                                if (go.activeSelf && go.GetComponent<GhostFx>() == null)
                                {
                                    var dist = Vector3.Distance(pos, go.transform.position);
                                    Log("           Distance to button: " + dist);
                                    if (dist < 30f)
                                    {
                                        if (go.GetComponent<AutoLaunchDelay>() == null)
                                        {
                                            Destroy(go.AddComponent<AutoLaunchDelay>(), 40f);
                                            Log("Launching " + wo.GetId() + ", " + wo.GetGroup().GetId() + ", " + go.transform.position);
                                            parent.OnAction();
                                        }
                                        else
                                        {
                                            Log("           Already launched");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                toRemove.Add(wo.GetId());
                            }
                        }
                        else
                        {
                            toRemove.Add(wo.GetId());
                        }
                    }
                    foreach (var id in toRemove)
                    {
                        if (rockets.TryGetValue(id, out var wo))
                        {
                            Log("Removing " + id + ", " + wo.GetGroup().GetId() + ", " + wo.GetPosition());
                            rockets.Remove(id);
                        }
                    }
                }
                

                yield return new WaitForSeconds(delay);
            }
        }
    }
}
