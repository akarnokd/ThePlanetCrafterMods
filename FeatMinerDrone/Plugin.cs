using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Reflection;
using System.IO;
using System.Collections;
using MijuTools;
using System.Linq;
using BepInEx.Configuration;
using System;
using BepInEx.Bootstrap;
using FeatMultiplayer;
using System.Collections.Concurrent;

namespace FeatMinerDrone
{
    [BepInPlugin("akarnokd.theplanetcraftermods.featspacecows", "(Feat) Miner Drone", "1.0.0.0")]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public partial class Plugin : BaseUnityPlugin
    {
        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ManualLogSource logger;

        static ConfigEntry<bool> debugMode;

        static ConfigEntry<string> droneContainerId;

        internal static FeatMultiplayerApi multiplayer;

        static Texture2D droneTexture;

        static int droneShadowContainerIdStart = 50000;

        static int droneShadowContainerIdEnd = droneShadowContainerIdStart + 10000;

        static readonly Dictionary<int, Drone> drones = new();

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            if (Config.Bind("General", "Enabled", true, "Enable this mod?").Value)
            {
                debugMode = Config.Bind("General", "DebugMode", true, "Enable debugging with detailed logs (chatty!).");
                droneContainerId = Config.Bind("General", "ContainerId", "*MinerDrone", "The container tag to look for");

                logger = Logger;

                Assembly me = Assembly.GetExecutingAssembly();
                string dir = Path.GetDirectoryName(me.Location);

                droneTexture = LoadPNG(Path.Combine(dir, "MinerDrone.png"));

                multiplayer = FeatMultiplayerApi.Create();
                if (multiplayer.IsAvailable()) { 
                    Logger.LogInfo("Mod " + modFeatMultiplayerGuid + " found, enabling multiplayer support");

                }

                Harmony.CreateAndPatchAll(typeof(Plugin));

                StartCoroutine(DroneCheckerLoop(1f));
            } 
            else
            {
                Logger.LogInfo($"Plugin is disabled via config.");
            }
        }

        static Texture2D LoadPNG(string filename)
        {
            Texture2D tex = new Texture2D(200, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        static void log(string s)
        {
            if (debugMode.Value)
            {
                logger.LogInfo(s);
            }
        }

        string DebugWorldObject(WorldObject wo)
        {
            if (wo != null)
            {
                var str = wo.GetId() + ", " + wo.GetGroup().GetId();
                var txt = wo.GetText();
                if (!string.IsNullOrEmpty(txt))
                {
                    str += ", \"" + txt + "\"";
                }
                str += ", " + (wo.GetIsPlaced() ? wo.GetPosition() : "");
                return str;
            }
            return "null";
        }

        IEnumerator DroneCheckerLoop(float delay)
        {
            for (; ; )
            {
                CheckDrones();
                yield return new WaitForSeconds(delay);
            }
        }

        void CheckDrones()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p == null || p.GetActivePlayerController() == null)
            {
                log("Player is not ready");
                return;
            }

            if (!multiplayer.IsAvailable() || multiplayer.GetState() != FeatMultiplayerApi.MultiplayerState.CoopClient)
            {
                CheckDronesLocal();
            }
            else
            {
                log("Multiplayer: we run on the client.");
            }
        }

        void CheckDronesLocal()
        {
            List<WorldObject> droneTargets = new();
            Dictionary<string, WorldObject> droneShadowInventories = new();
            int maxId = droneShadowContainerIdStart;

            string droneContainerStr = droneContainerId.Value;

            foreach (WorldObject wo in WorldObjectsHandler.GetConstructedWorldObjects())
            {
                if (wo.GetId() >= droneShadowContainerIdStart && wo.GetId() <= droneShadowContainerIdEnd)
                {
                    droneShadowInventories[wo.GetText()] = wo;
                    maxId = Math.Max(maxId, wo.GetId());
                }
                if (wo.GetText() == droneContainerStr)
                {
                    droneTargets.Add(wo);
                }
            }

            HashSet<int> existingTargets = new();

            foreach (var wo in droneTargets)
            {
                var id = wo.GetId();
                var woId = id.ToString();
                if (!droneShadowInventories.TryGetValue(woId, out var shadowContainer))
                {
                    log("Creating shadow container for target " + DebugWorldObject(wo));
                    shadowContainer = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Container1"), ++maxId);
                    log("         Shadow container " + DebugWorldObject(shadowContainer));
                    log("                inventory " + shadowContainer.GetLinkedInventoryId());
                }
                var inv = InventoriesHandler.GetInventoryById(shadowContainer.GetLinkedInventoryId());

                if (!drones.TryGetValue(id, out var drone))
                {
                    drone = Drone.CreateDrone(droneTexture, Color.white);
                    drone.parent = wo;
                    drone.inventory = inv;
                    drone.shadowContainer = shadowContainer;
                    drone.SetPosition(wo.GetPosition() + new Vector3(0, 1, 0), Quaternion.identity);
                    drones[id] = drone;
                }

                existingTargets.Add(id);

                ManageDrone(drone);
            }

            foreach (var drone in new List<KeyValuePair<int, Drone>>(drones))
            {
                if (!existingTargets.Contains(drone.Key))
                {
                    if (drone.Value.inventory != null)
                    {
                        InventoriesHandler.DestroyInventory(drone.Value.inventory.GetId());
                    }
                    if (drone.Value.shadowContainer != null)
                    {
                        WorldObjectsHandler.DestroyWorldObject(drone.Value.shadowContainer);
                    }
                    drone.Value.Destroy();
                    drones.Remove(drone.Key);
                }
            }
        }

        void ManageDrone(Drone drone)
        {

        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
            log("Clearing Drones = " + drones.Count);
            drones.Clear();
            log("                Done");
        }

    }
}
