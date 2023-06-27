using BepInEx;
using SpaceCraft;
using HarmonyLib;
using BepInEx.Configuration;
using System.Collections;
using System;
using BepInEx.Bootstrap;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;

namespace CheatAutoLaunchRocket
{
    [BepInPlugin("akarnokd.theplanetcraftermods.cheatautolaunchrocket", "(Cheat) Auto Launch Rockets", PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ConfigEntry<bool> isEnabled;

        static ConfigEntry<bool> debugMode;

        static Func<string> getMultiplayerMode;

        static ManualLogSource logger;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            isEnabled = Config.Bind("General", "Enabled", true, "Is the mod enabled?");
            debugMode = Config.Bind("General", "DebugMode", false, "Enable debugging with detailed logs (chatty!).");

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                Logger.LogInfo("Mod " + modFeatMultiplayerGuid + " found, managing multiplayer mode");

                getMultiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);

            }

            logger = Logger;

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

        static Dictionary<int, WorldObject> rockets = new();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "StoreNewWorldObject")]
        static void WorldObjectsHandler_StoreNewWorldObject(WorldObject _worldObject)
        {
            var gid = _worldObject.GetGroup().GetId();
            if (gid.StartsWith("Rocket") && gid != "RocketReactor")
            {
                rockets[_worldObject.GetId()] = _worldObject;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DestroyWorldObject))]
        static void WorldObjectsHandler_DestroyWorldObject(WorldObject _worldObject)
        {
            int id = _worldObject.GetId();
            if (!WorldObjectsIdHandler.IsWorldObjectFromScene(id))
            {
                var gid = _worldObject.GetGroup().GetId();
                if (gid.StartsWith("Rocket") && gid != "RocketReactor")
                {
                    rockets.Remove(id);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            rockets.Clear();
        }

        /// <summary>
        /// Marker class so the same rocket is not launched twice.
        /// </summary>
        class AutoLaunchDelay : MonoBehaviour
        {

        }

        static void log(string s)
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
                if (isEnabled.Value && (getMultiplayerMode == null || getMultiplayerMode() != "CoopClient"))
                {
                    log("Launch check for " + rockets.Count + " known rockets");
                    var pos = parent.transform.position;
                    List<int> toRemove = new();
                    foreach (var wo in rockets.Values)
                    {
                        log("       Rocket: " + wo.GetId() + ", " + wo.GetGroup().GetId() + ", " + wo.GetPosition());
                        if (wo.GetIsPlaced())
                        {
                            var go = wo.GetGameObject();
                            if (go != null)
                            {
                                if (go.activeSelf)
                                {
                                    var dist = Vector3.Distance(pos, go.transform.position);
                                    log("           Distance to button: " + dist);
                                    if (dist < 30f)
                                    {
                                        if (go.GetComponent<AutoLaunchDelay>() == null)
                                        {
                                            Destroy(go.AddComponent<AutoLaunchDelay>(), 40f);
                                            log("Launching " + wo.GetId() + ", " + wo.GetGroup().GetId() + ", " + go.transform.position);
                                            parent.OnAction();
                                        }
                                        else
                                        {
                                            log("           Already launched");
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
                            log("Removing " + id + ", " + wo.GetGroup().GetId() + ", " + wo.GetPosition());
                            rockets.Remove(id);
                        }
                    }
                }
                

                yield return new WaitForSeconds(delay);
            }
        }
    }
}
