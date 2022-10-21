using BepInEx;
using SpaceCraft;
using HarmonyLib;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using BepInEx.Configuration;
using System.IO;
using System;
using System.IO.Compression;
using System.Text;
using System.Collections;
using UnityEngine;
using MijuTools;
using BepInEx.Bootstrap;
using UnityEngine.InputSystem;
using System.Reflection;

namespace FeatTechniciansExile
{
    [BepInPlugin("akarnokd.theplanetcraftermods.feattechniciansexile", "(Feat) Technicians Exile", "0.1.0.0")]
    [BepInDependency(modFeatMultiplayerGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {

        const string modFeatMultiplayerGuid = "akarnokd.theplanetcraftermods.featmultiplayer";

        static ManualLogSource logger;

        internal static Texture2D technicianFront;
        internal static Texture2D technicianBack;

        static Func<string> multiplayerMode;

        static Vector3 technicianDropLocation = new Vector3(1995, 75.7f, 1073);

        static TechnicianAvatar avatar;

        static readonly int technicianWorldObjectIdStart = 70000;

        static WorldObject escapePod;
        static WorldObject livingPod;
        static WorldObject desk;
        static WorldObject bed;
        static WorldObject screen;
        static WorldObject grower;
        static WorldObject collector;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            logger = Logger;

            if (Chainloader.PluginInfos.TryGetValue(modFeatMultiplayerGuid, out var pi))
            {
                multiplayerMode = (Func<string>)AccessTools.Field(pi.Instance.GetType(), "apiGetMultiplayerMode").GetValue(null);
            }
            Assembly me = Assembly.GetExecutingAssembly();
            string dir = Path.GetDirectoryName(me.Location);

            technicianFront = LoadPNG(Path.Combine(dir, "Technician_Front.png"));
            technicianBack = LoadPNG(Path.Combine(dir, "Technician_Back.png"));

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static Texture2D LoadPNG(string filename)
        {
            Texture2D tex = new Texture2D(100, 200);
            tex.LoadImage(File.ReadAllBytes(filename));

            return tex;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SessionController), "Start")]
        static void SessionController_Start()
        {
            bool dontSaveMe = true;

            logger.LogInfo("Start");
            logger.LogInfo("  Finding the Escape Pod");
            escapePod = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart);
            if (escapePod == null)
            {
                logger.LogInfo("    Creating the Escape Pod");
                escapePod = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("EscapePod"), 
                    technicianWorldObjectIdStart);
                escapePod.SetPositionAndRotation(technicianDropLocation, Quaternion.identity * Quaternion.Euler(0, -90, 0));
                escapePod.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(escapePod, false);
            }

            var livingPodBase = technicianDropLocation + new Vector3(0, 0, 15);

            logger.LogInfo("  Finding the Living Pod");
            livingPod = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 1);
            if (livingPod == null)
            {
                logger.LogInfo("    Creating the Living Pod");
                livingPod = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("pod"), 
                    technicianWorldObjectIdStart + 1);
                livingPod.SetPositionAndRotation(livingPodBase, Quaternion.identity * Quaternion.Euler(0, 90, 0));
                livingPod.SetPanelsId(new List<int> { 1, 4, 1, 1, 5, 7 });
                livingPod.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(livingPod, false);
            }

            var livingPodFloor = livingPodBase + new Vector3(0, 1, 0);

            logger.LogInfo("  Finding the Bed");
            bed = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 2);
            if (bed == null)
            {
                logger.LogInfo("    Creating the Bed");
                bed = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("BedSimple"), 
                    technicianWorldObjectIdStart + 2);
                bed.SetPositionAndRotation(livingPodFloor, Quaternion.identity);
                bed.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(bed, false);
            }

            logger.LogInfo("  Finding the Desk");
            desk = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 3);
            if (desk == null)
            {
                logger.LogInfo("    Creating the Desk");
                desk = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("Desktop1"), 
                    technicianWorldObjectIdStart + 3);
                desk.SetPositionAndRotation(livingPodFloor, Quaternion.identity);
                desk.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(desk, false);
            }

            logger.LogInfo("  Finding the Rockets Screen");
            screen = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 4);
            if (screen == null)
            {
                logger.LogInfo("    Creating the Rockets Screen");
                screen = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("ScreenRockets"),
                    technicianWorldObjectIdStart + 4);
                screen.SetPositionAndRotation(livingPodFloor, Quaternion.identity);
                screen.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(screen, false);
            }

            logger.LogInfo("  Finding the Grower");
            grower = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 5);
            if (grower == null)
            {
                logger.LogInfo("    Creating the Grower");
                grower = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("VegetableGrower1"),
                    technicianWorldObjectIdStart + 5);
                grower.SetPositionAndRotation(livingPodFloor, Quaternion.identity);
                grower.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(grower, false);
            }

            logger.LogInfo("  Finding the Collector");
            collector = WorldObjectsHandler.GetWorldObjectViaId(technicianWorldObjectIdStart + 6);
            if (collector == null)
            {
                logger.LogInfo("    Creating the Collector");
                collector = WorldObjectsHandler.CreateNewWorldObject(GroupsHandler.GetGroupViaId("WaterCollector1"),
                    technicianWorldObjectIdStart + 6);
                collector.SetPositionAndRotation(livingPodBase + new Vector3(0, -1, 15), Quaternion.identity);
                collector.SetDontSaveMe(dontSaveMe);
                WorldObjectsHandler.InstantiateWorldObject(collector, false);
            }

            logger.LogInfo("  Creating the Technician");
            avatar = TechnicianAvatar.CreateAvatar(Color.white);
            avatar.SetPosition(technicianDropLocation + new Vector3(0, -0.5f, 0), Quaternion.identity * Quaternion.Euler(0, -90, 0));


            logger.LogInfo("  Limiting object interactions");
            Destroy(livingPod.GetGameObject().GetComponent<ActionDeconstructible>());
            Destroy(desk.GetGameObject().GetComponent<ActionDeconstructible>());
            Destroy(bed.GetGameObject().GetComponent<ActionDeconstructible>());
            Destroy(screen.GetGameObject().GetComponent<ActionDeconstructible>());
            Destroy(grower.GetGameObject().GetComponent<ActionDeconstructible>());
            Destroy(grower.GetGameObject().GetComponent<ActionnableInteractive>());
            Destroy(grower.GetGameObject().GetComponent<ActionOpenable>());
            Destroy(collector.GetGameObject().GetComponent<ActionDeconstructible>());
            Destroy(collector.GetGameObject().GetComponent<ActionnableInteractive>());
            Destroy(collector.GetGameObject().GetComponent<ActionOpenable>());

            logger.LogInfo("Done");
        }

        void Update()
        {
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowPause), nameof(UiWindowPause.OnQuit))]
        static void UiWindowPause_OnQuit()
        {
        }

    }
}
